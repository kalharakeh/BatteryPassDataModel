using BatteryPassWeb.Configuration;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

DotEnvLoader.Load(Path.Combine(AppContext.BaseDirectory, ".env.local"));
DotEnvLoader.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env.local"));

var builder = WebApplication.CreateBuilder(args);

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
    options.DemoAdminEmail = Environment.GetEnvironmentVariable("DEMO_ADMIN_EMAIL")
        ?? builder.Configuration["BatteryPass:DemoAdminEmail"]
        ?? "admin@example.test";
    options.DemoAdminPassword = Environment.GetEnvironmentVariable("DEMO_ADMIN_PASSWORD")
        ?? builder.Configuration["BatteryPass:DemoAdminPassword"]
        ?? "Password123!";
    options.ExternalApiEncryptionKey = Environment.GetEnvironmentVariable("EXTERNAL_API_ENCRYPTION_KEY")
        ?? builder.Configuration["BatteryPass:ExternalApiEncryptionKey"]
        ?? string.Empty;
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
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
    options.AddPolicy("ClusterAdminOrAdmin", policy => policy.RequireRole("clusterAdmin", "admin"));
});

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<PassportRepository>();
builder.Services.AddSingleton<ClusterRepository>();
builder.Services.AddSingleton<PassportViewModelFactory>();
builder.Services.AddSingleton<SchemaRegistryService>();
builder.Services.AddSingleton<JsonSchemaValidationService>();
builder.Services.AddSingleton<PassportValidationService>();
builder.Services.AddSingleton<PassportPublishPolicyService>();
builder.Services.AddSingleton<PassportReadinessService>();
builder.Services.AddSingleton<PassportEvidenceService>();
builder.Services.AddSingleton<DataCompletionPolicyService>();
builder.Services.AddSingleton<LocalAdminEditableFieldPolicyService>();
builder.Services.AddSingleton<ProductTemplateService>();
builder.Services.AddSingleton<DemoRequiredDataCompletionService>();
builder.Services.AddSingleton<PassportDataNormalizationService>();
builder.Services.AddSingleton<CanonicalPassportSnapshotService>();
builder.Services.AddSingleton<DemoSigningKeyService>();
builder.Services.AddSingleton<PassportTrustService>();
builder.Services.AddSingleton<AuditRevisionService>();
builder.Services.AddSingleton<PassportTrustWorkflowService>();
builder.Services.AddSingleton<AccessControlService>();
builder.Services.AddSingleton<PassportQrCodeService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<ExternalApiSecurityService>();
builder.Services.AddSingleton<ExternalApiRepository>();
builder.Services.AddSingleton<BatteryTelemetryRepository>();
builder.Services.AddSingleton<ExternalApiInitializer>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<ExternalApiInitializer>();
    await initializer.InitializeAsync();
}

app.Run();
