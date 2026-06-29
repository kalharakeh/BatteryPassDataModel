using BatteryPassWeb.Configuration;
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
var maxTelemetryPoints = ParsePositiveIntSetting(
    EnvironmentOrConfig(builder.Configuration, "MAX_TELEMETRY_POINTS", "MaxTelemetryPoints"),
    BatteryPassOptions.DefaultMaxTelemetryPoints);
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
    options.DemoAdminPassword = Environment.GetEnvironmentVariable("DEMO_ADMIN_PASSWORD")
        ?? builder.Configuration["BatteryPass:DemoAdminPassword"]
        ?? "Password123!";
    options.ExternalApiEncryptionKey = Environment.GetEnvironmentVariable("EXTERNAL_API_ENCRYPTION_KEY")
        ?? builder.Configuration["BatteryPass:ExternalApiEncryptionKey"]
        ?? string.Empty;
    options.IdGenerationSecret = Environment.GetEnvironmentVariable("ID_GENERATION_SECRET")
        ?? builder.Configuration["BatteryPass:IdGenerationSecret"]
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
    options.AppBaseUrl = Environment.GetEnvironmentVariable("APP_BASE_URL")
        ?? Environment.GetEnvironmentVariable("APP_URL")
        ?? builder.Configuration["BatteryPass:AppBaseUrl"]
        ?? builder.Configuration["BatteryPass:AppUrl"]
        ?? string.Empty;
    options.RequireHttpsRedirection = DeploymentSecurityConfiguration.ParseRequireHttpsRedirection(
        Environment.GetEnvironmentVariable("REQUIRE_HTTPS_REDIRECTION")
            ?? builder.Configuration["BatteryPass:RequireHttpsRedirection"]);
    options.EnableDemoData = ParseFeatureFlag(
        Environment.GetEnvironmentVariable("ENABLE_DEMO_DATA")
            ?? builder.Configuration["BatteryPass:EnableDemoData"]);
    options.EnablePublicDemoReadToken = ParseFeatureFlag(
        Environment.GetEnvironmentVariable("ENABLE_PUBLIC_DEMO_READ_TOKEN")
            ?? builder.Configuration["BatteryPass:EnablePublicDemoReadToken"]);
    options.EnableDemoWriteSignTesting = ParseFeatureFlag(
        Environment.GetEnvironmentVariable("ENABLE_DEMO_WRITE_SIGN_TESTING")
            ?? builder.Configuration["BatteryPass:EnableDemoWriteSignTesting"]);
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
    options.MaxTelemetryPoints = maxTelemetryPoints;
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
        options.AccessDeniedPath = "/login";
        options.Cookie.Name = "battery-pass-demo-session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(BatteryPassOptions.DefaultSessionTimeoutMinutes);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = context =>
            context.HttpContext.RequestServices
                .GetRequiredService<AuthenticationSessionService>()
                .ValidatePrincipalAsync(context);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
    options.AddPolicy("ClusterAdminOrAdmin", policy => policy.RequireRole("clusterAdmin", "admin"));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var response = context.HttpContext.Response;
        if (!response.HasStarted)
        {
            response.StatusCode = StatusCodes.Status429TooManyRequests;
            await response.WriteAsJsonAsync(new { error = "Too many requests." }, cancellationToken);
        }
    };

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
    options.AddPolicy(SecurityRateLimitPolicyNames.FileUpload, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            FileUploadRateLimitPartitionKey(context),
            _ => FixedWindowOptions(fileUploadRateLimitPerWindow, rateLimitWindowSeconds)));
});

builder.Services.AddControllersWithViews();
var trustedProxyNetworks = builder.Configuration["TRUSTED_PROXY_NETWORKS"];
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    ElasticBeanstalkForwardedHeaders.Configure(options, trustedProxyNetworks));
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<PassportRepository>();
builder.Services.AddSingleton<BatteryRepository>();
builder.Services.AddSingleton<BatteryTableService>();
builder.Services.AddSingleton<BatteryPassportSnapshotService>();
builder.Services.AddSingleton<BatteryPassportDeltaService>();
builder.Services.AddSingleton<BatteryTemplateUpdateService>();
builder.Services.AddSingleton<BatteryRouteResolutionService>();
builder.Services.AddSingleton<ClusterRepository>();
builder.Services.AddSingleton<PassportViewModelFactory>();
builder.Services.AddSingleton<SchemaRegistryService>();
builder.Services.AddSingleton<JsonSchemaValidationService>();
builder.Services.AddSingleton<PassportValidationService>();
builder.Services.AddSingleton<PassportPublishPolicyService>();
builder.Services.AddSingleton<PassportReadinessService>();
builder.Services.AddSingleton<PassportEvidenceService>();
builder.Services.AddSingleton<DataCompletionPolicyService>();
builder.Services.AddSingleton<EditableFieldPolicyService>();
builder.Services.AddSingleton<LocalAdminEditableFieldPolicyService>();
builder.Services.AddSingleton<ProductTemplateService>();
builder.Services.AddSingleton<DemoRequiredDataCompletionService>();
builder.Services.AddSingleton<PassportDataNormalizationService>();
builder.Services.AddSingleton<CanonicalPassportSnapshotService>();
builder.Services.AddSingleton<DemoSigningKeyService>();
builder.Services.AddSingleton<PassportTrustService>();
builder.Services.AddSingleton<AuditRevisionService>();
builder.Services.AddSingleton<BatteryAuditService>();
builder.Services.AddSingleton<ApplicationAuditService>();
builder.Services.AddSingleton<ApplicationSettingsService>();
builder.Services.AddSingleton<AuthenticationSessionService>();
builder.Services.AddSingleton<BatteryCreationService>();
builder.Services.AddSingleton<PassportTrustWorkflowService>();
builder.Services.AddSingleton<AccessControlService>();
builder.Services.AddSingleton<PassportQrCodeService>();
builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BatteryPassOptions>>().Value;
    var environment = provider.GetRequiredService<IWebHostEnvironment>();
    return new BatteryIdService(options.IdGenerationSecret, environment.IsDevelopment());
});
builder.Services.AddSingleton<AuthService>();
builder.Services.AddHttpClient<IEmailSender, PowerAutomateEmailSender>();
builder.Services.AddSingleton<ExternalApiSecurityService>();
builder.Services.AddSingleton<ExternalApiRepository>();
builder.Services.AddSingleton<BatteryTelemetryRepository>();
builder.Services.AddSingleton<ExternalApiInitializer>();

var app = builder.Build();
var batteryPassOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<BatteryPassOptions>>().Value;

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    if (batteryPassOptions.RequireHttpsRedirection)
    {
        app.UseHsts();
    }
}

app.UseForwardedHeaders();
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
            $"Request body is too large. Maximum allowed size is {maxBodyBytes} bytes.");
        return;
    }

    await next();
});
app.UseStaticFiles();
app.UseRouting();
if (batteryPassOptions.EnableRateLimiting)
{
    app.UseRateLimiter();
}
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
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

static string FileUploadRateLimitPartitionKey(HttpContext context)
{
    if (context.User.Identity?.IsAuthenticated == true
        && !string.IsNullOrWhiteSpace(context.User.Identity.Name))
    {
        return $"upload-user:{HashForRateLimitPartition(context.User.Identity.Name)}";
    }

    return $"upload-ip:{RemoteIpPartitionKey(context)}";
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

static long ConfiguredBodyLimitFor(HttpRequest request, BatteryPassOptions options)
{
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
