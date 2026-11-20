using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

var currentDirectory = Directory.GetCurrentDirectory();
DotEnvLoader.Load(Path.Combine(AppContext.BaseDirectory, ".env.local"));
DotEnvLoader.Load(Path.Combine(currentDirectory, ".env.local"));
DotEnvLoader.Load(Path.Combine(currentDirectory, "web", ".env.local"));

var builder = WebApplication.CreateBuilder(args);

if (DeploymentSecurityConfiguration.MissingDatabaseName(builder.Environment.IsProduction(), Environment.GetEnvironmentVariable("MONGODB_DB")) is { } missingDatabase)
{
    throw new InvalidOperationException(missingDatabase);
}

if (DemoAdminFallbackConfiguration.ProductionRefusal(
        builder.Environment.IsProduction(),
        ParseFeatureFlag(EnvironmentOrConfig(builder.Configuration, "ENABLE_DEMO_ADMIN_FALLBACK", "EnableDemoAdminFallback"))) is { } fallbackInProduction)
{
    throw new InvalidOperationException(fallbackInProduction);
}

if (BatteryPassHealthCheck.ProductionRefusal(builder.Environment.IsProduction(), builder.Configuration[BatteryPassHealthCheck.DiagnosticsEnvironmentVariable]) is { } diagnosticsInProduction)
{
    throw new InvalidOperationException(diagnosticsInProduction);
}

// The QR code's address and the battery IDs' address must be one site (finding D18); outside Production a warning.
// A setting that is there but empty counts as not set (C3 round three, point 1).
var appBaseUrlSetting = SiteAddressSettings.AppBaseUrl(Environment.GetEnvironmentVariable, builder.Configuration);
var idBaseUrlSetting = SiteAddressSettings.IdBaseUrl(Environment.GetEnvironmentVariable, builder.Configuration);

// Production needs the site's own signing key and two whole http(s) addresses (the manager's decision 61).
if (DemoSigningKeyService.ProductionKeyProblem(builder.Environment.IsProduction(), Environment.GetEnvironmentVariable(DemoSigningKeyService.PrivateKeySetting)) is { } missingSigningKey)
{
    throw new InvalidOperationException(missingSigningKey);
}

if (DppLinks.ProductionAddressProblem(builder.Environment.IsProduction(), appBaseUrlSetting, idBaseUrlSetting) is { } addressProblem)
{
    throw new InvalidOperationException(addressProblem);
}

if (DppLinks.HostMismatch(appBaseUrlSetting, idBaseUrlSetting) is { } hostMismatch && builder.Environment.IsProduction())
{
    throw new InvalidOperationException(hostMismatch);
}

// Older public keys the app still trusts after a key change (finding R23): an unreadable entry stops Production.
var trustedSigningKeysSetting = EnvironmentOrConfig(builder.Configuration, DemoSigningKeyService.TrustedKeysSetting, "SigningTrustedPublicKeys");
if (DemoSigningKeyService.UnreadableTrustedKeys(trustedSigningKeysSetting) is { } unreadableTrustedKeys && builder.Environment.IsProduction())
{
    throw new InvalidOperationException(unreadableTrustedKeys);
}

var enableRateLimiting = ParseFeatureFlag(
    EnvironmentOrConfig(builder.Configuration, "ENABLE_RATE_LIMITING", "EnableRateLimiting"),
    BatteryPassOptions.DefaultEnableRateLimiting);
var rateLimitWindowSeconds = ParsePositiveIntSetting(
    EnvironmentOrConfig(builder.Configuration, "RATE_LIMIT_WINDOW_SECONDS", "RateLimitWindowSeconds"),
    BatteryPassOptions.DefaultRateLimitWindowSeconds);
var loginRateLimitPerWindow = ParsePositiveIntSetting(
    EnvironmentOrConfig(builder.Configuration, "LOGIN_RATE_LIMIT_PER_WINDOW", "LoginRateLimitPerWindow"),
    BatteryPassOptions.DefaultLoginRateLimitPerWindow);
var externalApiReadRateLimitPerWindow = ParsePositiveIntSetting(
    EnvironmentOrConfig(builder.Configuration, "EXTERNAL_API_READ_RATE_LIMIT_PER_WINDOW", "ExternalApiReadRateLimitPerWindow"),
    BatteryPassOptions.DefaultExternalApiReadRateLimitPerWindow);
var externalApiWriteRateLimitPerWindow = ParsePositiveIntSetting(
    EnvironmentOrConfig(builder.Configuration, "EXTERNAL_API_WRITE_RATE_LIMIT_PER_WINDOW", "ExternalApiWriteRateLimitPerWindow"),
    BatteryPassOptions.DefaultExternalApiWriteRateLimitPerWindow);
var externalApiLifecycleRateLimitPerWindow = ParsePositiveIntSetting(
    EnvironmentOrConfig(builder.Configuration, "EXTERNAL_API_LIFECYCLE_RATE_LIMIT_PER_WINDOW", "ExternalApiLifecycleRateLimitPerWindow"),
    BatteryPassOptions.DefaultExternalApiLifecycleRateLimitPerWindow);
var fileUploadRateLimitPerWindow = ParsePositiveIntSetting(
    EnvironmentOrConfig(builder.Configuration, "FILE_UPLOAD_RATE_LIMIT_PER_WINDOW", "FileUploadRateLimitPerWindow"),
    BatteryPassOptions.DefaultFileUploadRateLimitPerWindow);
var maxAuthorizationHeaderBytes = ParsePositiveIntSetting(
    EnvironmentOrConfig(builder.Configuration, "MAX_AUTHORIZATION_HEADER_BYTES", "MaxAuthorizationHeaderBytes"),
    BatteryPassOptions.DefaultMaxAuthorizationHeaderBytes);
var maxJsonBodyBytes = ParsePositiveLongSetting(
    EnvironmentOrConfig(builder.Configuration, "MAX_JSON_BODY_BYTES", "MaxJsonBodyBytes"),
    BatteryPassOptions.DefaultMaxJsonBodyBytes);
var maxUploadBytes = ParsePositiveLongSetting(
    EnvironmentOrConfig(builder.Configuration, "MAX_UPLOAD_BYTES", "MaxUploadBytes"),
    BatteryPassOptions.DefaultMaxUploadBytes);

builder.Services.Configure<BatteryPassOptions>(options =>
{
    options.MongoDbUri = Environment.GetEnvironmentVariable("MONGODB_URI")
        ?? builder.Configuration["BatteryPass:MongoDbUri"]
        ?? string.Empty;
    options.MongoDbName = Environment.GetEnvironmentVariable("MONGODB_DB")
        ?? builder.Configuration["BatteryPass:MongoDbName"]
        ?? "battery_pass_demo";
    options.SessionSecret = Environment.GetEnvironmentVariable("SESSION_SECRET")
        ?? builder.Configuration["BatteryPass:SessionSecret"]
        ?? string.Empty;
    var sessionTimeoutText = Environment.GetEnvironmentVariable("SESSION_TIMEOUT_MINUTES")
        ?? builder.Configuration["BatteryPass:SessionTimeoutMinutes"];
    options.SessionTimeoutMinutes = int.TryParse(sessionTimeoutText, out var sessionTimeoutMinutes)
        ? BatteryPassOptions.NormalizeSessionTimeoutMinutes(sessionTimeoutMinutes)
        : BatteryPassOptions.DefaultSessionTimeoutMinutes;
    options.DemoAdminEmail = Environment.GetEnvironmentVariable("DEMO_ADMIN_EMAIL")
        ?? builder.Configuration["BatteryPass:DemoAdminEmail"]
        ?? "admin@example.test";
    // No default: the fallback admin works only with a password the environment gives (finding R21).
    options.DemoAdminPassword = Environment.GetEnvironmentVariable("DEMO_ADMIN_PASSWORD")
        ?? builder.Configuration["BatteryPass:DemoAdminPassword"]
        ?? string.Empty;
    options.DemoUsersPassword = Environment.GetEnvironmentVariable("DEMO_USERS_PASSWORD")
        ?? builder.Configuration["BatteryPass:DemoUsersPassword"];
    options.ExternalApiEncryptionKey = Environment.GetEnvironmentVariable("EXTERNAL_API_ENCRYPTION_KEY")
        ?? builder.Configuration["BatteryPass:ExternalApiEncryptionKey"]
        ?? string.Empty;
    options.PowerAutomateResetWebhookUrl = Environment.GetEnvironmentVariable("POWER_AUTOMATE_RESET_WEBHOOK_URL")
        ?? builder.Configuration["BatteryPass:PowerAutomateResetWebhookUrl"]
        ?? string.Empty;
    options.PowerAutomateResetWebhookSecret = Environment.GetEnvironmentVariable("POWER_AUTOMATE_RESET_WEBHOOK_SECRET")
        ?? builder.Configuration["BatteryPass:PowerAutomateResetWebhookSecret"]
        ?? string.Empty;
    options.PasswordResetAppName = Environment.GetEnvironmentVariable("PASSWORD_RESET_APP_NAME")
        ?? builder.Configuration["BatteryPass:PasswordResetAppName"]
        ?? "Battery Pass";
    options.IdAgency = (Environment.GetEnvironmentVariable("DPP_ID_AGENCY") ?? builder.Configuration["BatteryPass:IdAgency"] ?? string.Empty).Trim();
    options.IdCompany = (Environment.GetEnvironmentVariable("DPP_ID_COMPANY") ?? builder.Configuration["BatteryPass:IdCompany"] ?? string.Empty).Trim();
    options.IdBaseUrl = (idBaseUrlSetting ?? string.Empty).Trim().TrimEnd('/');
    options.AppBaseUrl = appBaseUrlSetting ?? string.Empty;
    options.RequireHttpsRedirection = DeploymentSecurityConfiguration.ParseRequireHttpsRedirection(
        Environment.GetEnvironmentVariable("REQUIRE_HTTPS_REDIRECTION")
            ?? builder.Configuration["BatteryPass:RequireHttpsRedirection"]);
    options.EnableDemoAdminFallback = ParseFeatureFlag(
        Environment.GetEnvironmentVariable("ENABLE_DEMO_ADMIN_FALLBACK")
            ?? builder.Configuration["BatteryPass:EnableDemoAdminFallback"]);
    options.EnableRateLimiting = enableRateLimiting;
    options.RateLimitWindowSeconds = rateLimitWindowSeconds;
    options.LoginRateLimitPerWindow = loginRateLimitPerWindow;
    options.ExternalApiReadRateLimitPerWindow = externalApiReadRateLimitPerWindow;
    options.ExternalApiWriteRateLimitPerWindow = externalApiWriteRateLimitPerWindow;
    options.ExternalApiLifecycleRateLimitPerWindow = externalApiLifecycleRateLimitPerWindow;
    options.FileUploadRateLimitPerWindow = fileUploadRateLimitPerWindow;
    options.MaxAuthorizationHeaderBytes = maxAuthorizationHeaderBytes;
    options.MaxJsonBodyBytes = maxJsonBodyBytes;
    options.MaxUploadBytes = maxUploadBytes;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxUploadBytes + 65_536;
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/not-allowed";
        options.Cookie.Name = "battery-pass-demo-session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(BatteryPassOptions.DefaultSessionTimeoutMinutes);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = context =>
            context.HttpContext.RequestServices
                .GetRequiredService<AuthenticationSessionService>()
                .ValidatePrincipalAsync(context);
        // The TempData cookie is the browser's: one person's battery messages never reach the next (owner's step-5 test).
        options.Events.OnSigningIn = context => CardMessages.ForgetAsync(context.HttpContext);
        options.Events.OnSigningOut = context => CardMessages.ForgetAsync(context.HttpContext);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
    options.AddPolicy("ClusterAdminOrAdmin", policy => policy.RequireRole("clusterAdmin", "admin"));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // JSON for the API and the passports' .json addresses, a sentence for a person on a page (review 2 of N1, point 6).
    options.OnRejected = TooManyRequests.WriteAsync;

    options.AddPolicy(SecurityRateLimitPolicyNames.Login, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            LoginRateLimitPartitionKey(context),
            _ => FixedWindowOptions(loginRateLimitPerWindow, rateLimitWindowSeconds)));
    options.AddPolicy(SecurityRateLimitPolicyNames.ExternalApiRead, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            ExternalApiRateLimitPartitionKey(context),
            _ => FixedWindowOptions(externalApiReadRateLimitPerWindow, rateLimitWindowSeconds)));
    options.AddPolicy(SecurityRateLimitPolicyNames.ExternalApiWrite, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            ExternalApiRateLimitPartitionKey(context),
            _ => FixedWindowOptions(externalApiWriteRateLimitPerWindow, rateLimitWindowSeconds)));
    options.AddPolicy(SecurityRateLimitPolicyNames.ExternalApiLifecycle, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            ExternalApiRateLimitPartitionKey(context),
            _ => FixedWindowOptions(externalApiLifecycleRateLimitPerWindow, rateLimitWindowSeconds)));
    // Counted per signed-in person, by address for the rest (FU4): needs the limiter to run after authentication, below.
    UploadRateLimit.Add(options, fileUploadRateLimitPerWindow, rateLimitWindowSeconds);

    // A passport's public data takes no token: counted by the caller's address only (C3 round two, point 5).
    PublicDataRateLimit.Add(options, externalApiReadRateLimitPerWindow, rateLimitWindowSeconds);
});

builder.Services.AddControllersWithViews();
var trustedProxyNetworks = builder.Configuration["TRUSTED_PROXY_NETWORKS"];
var trustedProxyHopLimit = builder.Configuration["TRUSTED_PROXY_HOP_LIMIT"];
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    ElasticBeanstalkForwardedHeaders.Configure(options, trustedProxyNetworks, trustedProxyHopLimit));

// UseHttpsRedirection has to know which port to redirect to. It looks for an
// explicit HttpsPort, an HTTPS_PORT configuration value, or an https:// address
// Kestrel is bound to. Behind Elastic Beanstalk none of those exist - TLS
// terminates at the load balancer and Kestrel is started with plain
// "--urls http://localhost:5000" - so without this the middleware silently
// passes every request through and no redirect ever happens.
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = HttpsRedirectionDefaults.PublicHttpsPort;

    // Deliberately a temporary redirect. A 301 is cached hard by browsers, which
    // would make disabling REQUIRE_HTTPS_REDIRECTION ineffective for anyone who
    // had already visited. 307 keeps the setting a genuine off switch.
    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
});
// ENABLE_DEMO_DATA (finding R03): on outside Production unless false, off in Production unless true.
builder.Services.AddSingleton(new BatteryPassWeb.Dpp.Demo.DemoDataSwitch(BatteryPassWeb.Dpp.Demo.DemoDataSwitch.Parse(
    EnvironmentOrConfig(builder.Configuration, "ENABLE_DEMO_DATA", "DemoDataEnabled"),
    builder.Environment.IsProduction())));
builder.Services.AddSingleton<MongoContext>();
// The sign-in and form keys live in the database, so a restart signs nobody out (finding S09).
// Encrypted with a secret made from the signing key, which is not in the database (PL round two).
builder.Services.AddBatteryPassDataProtection(builder.Environment.EnvironmentName, DemoSigningKeyService.DataProtectionSecret(Environment.GetEnvironmentVariable(DemoSigningKeyService.PrivateKeySetting)));
builder.Services.AddSingleton<ClusterRepository>();
// The issuer is the battery IDs' site (finding R23); without DPP_ID_BASE_URL, the app's own address.
builder.Services.AddSingleton(_ => new DemoSigningKeyService(idBaseUrlSetting, appBaseUrlSetting, trustedSigningKeysSetting));
builder.Services.AddSingleton<ApplicationAuditService>();
builder.Services.AddSingleton<ApplicationSettingsService>();
builder.Services.AddSingleton<AuthenticationSessionService>();
builder.Services.AddSingleton<AccessControlService>();
builder.Services.AddSingleton<PassportQrCodeService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<FirstTeamAccount>();
builder.Services.AddHttpClient<IEmailSender, PowerAutomateEmailSender>();
builder.Services.AddSingleton<ExternalApiSecurityService>();
builder.Services.AddSingleton<ExternalApiRepository>();
builder.Services.AddSingleton<ExternalApiInitializer>();
builder.Services.AddDpp();
builder.Services.AddSingleton<BatteryPassWeb.Dpp.Demo.DemoDataService>();

var app = builder.Build();
var batteryPassOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<BatteryPassOptions>>().Value;

// The security headers go on every response, so they come first (finding R24).
app.UseSecurityHeaders();

// The error page outside Development, and the "not found" page after the routes (finding U04).
app.UseErrorPages();

var healthDiagnosticsEnabled = BatteryPassHealthCheck.DiagnosticsEnabled(
    builder.Configuration[BatteryPassHealthCheck.DiagnosticsEnvironmentVariable]);

// Snapshot the proxy headers before UseForwardedHeaders consumes them. Afterwards
// they read as absent whether they were never sent, or were applied and stripped,
// and those two cases call for completely different fixes.
if (healthDiagnosticsEnabled)
{
    app.Use(async (context, next) =>
    {
        context.Items[BatteryPassHealthCheck.RawForwardedHeadersKey] = context.Request.Headers
            .Where(header => BatteryPassHealthCheck.IsProxyHeader(header.Key))
            .Select(header => new KeyValuePair<string, string>(header.Key, header.Value.ToString()))
            .ToList();

        await next();
    });
}

// nginx overwrites X-Forwarded-Proto with its own scheme, which is always http
// because TLS terminates at the load balancer. X-Forwarded-Port survives intact, so
// restore the scheme from it before UseForwardedHeaders runs - the corrected value
// then passes through the same trusted-proxy checks as any other forwarded header.
app.Use(async (context, next) =>
{
    if (ElasticBeanstalkForwardedHeaders.ShouldRestoreHttpsScheme(
            context.Request.Headers["X-Forwarded-Port"].ToString(),
            context.Request.Headers["X-Forwarded-Proto"].ToString()))
    {
        context.Request.Headers["X-Forwarded-Proto"] = "https";
    }

    await next();
});

app.UseForwardedHeaders();

// HSTS must run after UseForwardedHeaders. It only emits its header when the
// request is recognised as HTTPS, and behind the load balancer that is only true
// once X-Forwarded-Proto has been applied - nginx reaches Kestrel over plain HTTP.
// Registered earlier, it silently never fires.
if (!app.Environment.IsDevelopment() && batteryPassOptions.RequireHttpsRedirection)
{
    app.UseHsts();
}

// The load balancer health check reaches the instance over plain HTTP. It is
// answered here, ahead of the HTTPS redirect, so it always sees a 200 rather than
// a 301 - a redirected health check marks every target unhealthy and takes the
// whole environment out of service.
//
// This deliberately touches no database or external dependency. A degraded
// backend should surface as errors on real requests, not as instances being
// cycled out of the load balancer.
app.Map(BatteryPassHealthCheck.Path, healthApp => healthApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status200OK;
    context.Response.ContentType = "text/plain";

    if (!healthDiagnosticsEnabled)
    {
        await context.Response.WriteAsync(BatteryPassHealthCheck.ResponseBody);
        return;
    }

    // Never redirected, so this still answers while a redirect loop is in progress -
    // which is exactly when knowing what the application sees matters most.
    await context.Response.WriteAsync(BatteryPassHealthCheck.BuildDiagnostics(
        context.Request.Scheme,
        context.Request.IsHttps,
        context.Request.Headers["X-Forwarded-Proto"].ToString(),
        context.Request.Headers["X-Forwarded-For"].ToString(),
        context.Connection.RemoteIpAddress?.ToString(),
        context.Items[BatteryPassHealthCheck.RawForwardedHeadersKey]
            as IEnumerable<KeyValuePair<string, string>>));
}));

if (batteryPassOptions.RequireHttpsRedirection)
{
    app.UseHttpsRedirection();
}
app.Use(async (context, next) =>
{
    if (AuthorizationHeaderTooLarge(context, batteryPassOptions.MaxAuthorizationHeaderBytes))
    {
        await WriteRequestLimitErrorAsync(
            context,
            StatusCodes.Status431RequestHeaderFieldsTooLarge,
            "Authorization header is too large.");
        return;
    }

    ApplyConfiguredMaxRequestBodySize(context, batteryPassOptions);
    var maxBodyBytes = ConfiguredBodyLimitFor(context.Request, batteryPassOptions);
    if (maxBodyBytes > 0
        && context.Request.ContentLength.HasValue
        && context.Request.ContentLength.Value > maxBodyBytes)
    {
        await WriteRequestLimitErrorAsync(
            context,
            StatusCodes.Status413PayloadTooLarge,
            BatteryPassWeb.Dpp.Loading.LoadLimits.AppliesTo(context.Request) ? BatteryPassWeb.Dpp.Loading.LoadLimits.TooLarge : BatteryPassWeb.Dpp.Documents.UploadLimitWords.RequestTooLarge(NamedBodyLimitFor(context.Request, batteryPassOptions, maxBodyBytes)));
        return;
    }

    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
// After authentication, so that the upload limit knows the person (FU4); the sign-in and API limits count by address or token.
if (batteryPassOptions.EnableRateLimiting)
{
    app.UseRateLimiter();
}
app.UseAuthorization();

app.MapControllers();
app.MapNotFoundPage();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DppStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<DocumentStore>().EnsureIndexesAsync();
    // The first team account on a database that has none (decision 78); the log says only whether it was made or ignored.
    var firstTeamAccount = await scope.ServiceProvider.GetRequiredService<FirstTeamAccount>()
        .RunAsync(EnvironmentOrConfig(builder.Configuration, FirstTeamAccount.Setting, "FirstTeamAccountEmail"));
    if (firstTeamAccount.LogLine is { } firstTeamAccountLine)
    {
        app.Logger.LogInformation("{FirstTeamAccount}", firstTeamAccountLine);
    }
    if (DemoSigningKeyService.DataProtectionSecret(Environment.GetEnvironmentVariable(DemoSigningKeyService.PrivateKeySetting)) is null)
    {
        app.Logger.LogWarning(DataProtectionSetup.NotEncryptedWarning);
    }
    else if (app.Environment.IsProduction()
        && new MongoXmlRepository(scope.ServiceProvider.GetRequiredService<MongoContext>(), app.Environment.EnvironmentName, null, production: true).UnencryptedKeyCount() is > 0 and var unencryptedKeys)
    {
        app.Logger.LogWarning(
            "{Count} sign-in keys in the database are not encrypted (from an earlier version, or from a developer's PC without a signing key). This site never uses them; they can be deleted from {Collection}.",
            unencryptedKeys,
            MongoXmlRepository.CollectionName);
    }
    var fallbackOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BatteryPassOptions>>().Value;
    if (fallbackOptions.EnableDemoAdminFallback && string.IsNullOrWhiteSpace(fallbackOptions.DemoAdminPassword))
    {
        app.Logger.LogWarning(DemoAdminFallbackConfiguration.NoPasswordWarning);
    }
    if (!scope.ServiceProvider.GetRequiredService<DppLinks>().IsConfigured)
    {
        app.Logger.LogWarning("APP_BASE_URL is not set: battery packs can't be created and documents can't be linked until it is.");
    }
    if (DemoSigningKeyService.UnreadableTrustedKeys(trustedSigningKeysSetting) is { } trustedKeysWarning)
    {
        app.Logger.LogWarning(trustedKeysWarning);
    }
    var signingKeys = scope.ServiceProvider.GetRequiredService<DemoSigningKeyService>();
    app.Logger.LogInformation(
        "Passports are signed as {Issuer}, key {KeyId}. The key is published at {DidDocumentPath}. After a key change, add this public key to {TrustedKeysSetting}: {PublicKey}",
        signingKeys.Issuer,
        signingKeys.KeyId,
        DemoSigningKeyService.DidDocumentPath,
        DemoSigningKeyService.TrustedKeysSetting,
        signingKeys.PublicKeyText());
    if (DppLinks.HostMismatch(fallbackOptions.AppBaseUrl, fallbackOptions.IdBaseUrl) is { } addressWarning)
    {
        app.Logger.LogWarning(addressWarning);
    }
    var initializer = scope.ServiceProvider.GetRequiredService<ExternalApiInitializer>();
    await initializer.InitializeAsync();
}

app.Run();

static string? EnvironmentOrConfig(ConfigurationManager configuration, string environmentName, string optionName) =>
    Environment.GetEnvironmentVariable(environmentName)
    ?? configuration[$"BatteryPass:{optionName}"];

static bool ParseFeatureFlag(string? value, bool fallback = false)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return fallback;
    }

    var normalized = value.Trim();
    return normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
        || normalized.Equals("1", StringComparison.OrdinalIgnoreCase)
        || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase)
        || normalized.Equals("on", StringComparison.OrdinalIgnoreCase);
}

static int ParsePositiveIntSetting(string? value, int fallback) =>
    int.TryParse(value, out var parsed)
        ? BatteryPassOptions.NormalizePositiveInt(parsed, fallback)
        : fallback;

static long ParsePositiveLongSetting(string? value, long fallback) =>
    long.TryParse(value, out var parsed)
        ? BatteryPassOptions.NormalizePositiveLong(parsed, fallback)
        : fallback;

static FixedWindowRateLimiterOptions FixedWindowOptions(int permitLimit, int windowSeconds) => new()
{
    PermitLimit = permitLimit,
    Window = TimeSpan.FromSeconds(windowSeconds),
    QueueLimit = 0,
    AutoReplenishment = true
};

static string LoginRateLimitPartitionKey(HttpContext context) =>
    $"login:{RemoteIpPartitionKey(context)}";

static string ExternalApiRateLimitPartitionKey(HttpContext context)
{
    var authorization = context.Request.Headers.Authorization.FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(authorization))
    {
        return $"external-token:{HashForRateLimitPartition(authorization.Trim())}";
    }

    return $"external-ip:{RemoteIpPartitionKey(context)}";
}

static string RemoteIpPartitionKey(HttpContext context) =>
    context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

static string HashForRateLimitPartition(string value)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
    return WebEncoders.Base64UrlEncode(hash);
}

static bool AuthorizationHeaderTooLarge(HttpContext context, int maxAuthorizationHeaderBytes)
{
    if (maxAuthorizationHeaderBytes <= 0 || context.Request.Headers.Authorization.Count == 0)
    {
        return false;
    }

    var totalBytes = context.Request.Headers.Authorization
        .Sum(value => Encoding.UTF8.GetByteCount(value ?? string.Empty));
    return totalBytes > maxAuthorizationHeaderBytes;
}

static void ApplyConfiguredMaxRequestBodySize(HttpContext context, BatteryPassOptions options)
{
    var maxBodyBytes = ConfiguredBodyLimitFor(context.Request, options);
    if (maxBodyBytes <= 0)
    {
        return;
    }

    var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
    if (feature is { IsReadOnly: false })
    {
        feature.MaxRequestBodySize = maxBodyBytes;
    }
}

// The limit a person is told: a file upload's own limit (the body is allowed a little more for the form's other fields), not the body cap.
static long NamedBodyLimitFor(HttpRequest request, BatteryPassOptions options, long bodyCap) =>
    !BatteryPassWeb.Dpp.Loading.LoadLimits.AppliesTo(request) && !request.HasJsonContentType() && request.HasFormContentType
        && (request.ContentType?.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase) ?? false)
        ? options.MaxUploadBytes
        : bodyCap;

static long ConfiguredBodyLimitFor(HttpRequest request, BatteryPassOptions options)
{
    // A product data load sends the workbook with all its files at once (decision 83); each file still passes the upload limit.
    if (BatteryPassWeb.Dpp.Loading.LoadLimits.AppliesTo(request))
    {
        return BatteryPassWeb.Dpp.Loading.LoadLimits.MaxRequestBytes;
    }

    if (request.HasJsonContentType())
    {
        return options.MaxJsonBodyBytes;
    }

    if (request.HasFormContentType
        && (request.ContentType?.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase) ?? false))
    {
        return options.MaxUploadBytes + 65_536;
    }

    return 0;
}

static Task WriteRequestLimitErrorAsync(HttpContext context, int statusCode, string error)
{
    context.Response.StatusCode = statusCode;
    return context.Response.WriteAsJsonAsync(new { error });
}
