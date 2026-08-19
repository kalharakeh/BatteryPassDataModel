using BatteryPassWeb.Configuration;

namespace BatteryPassWeb.Tests;

public sealed class HealthCheckRedirectExemptionTests
{
    [Fact]
    public void HealthCheckPath_ShouldBeAStableRootLevelPath()
    {
        Assert.Equal("/health", BatteryPassHealthCheck.Path);
        Assert.StartsWith("/", BatteryPassHealthCheck.Path);
        Assert.NotEmpty(BatteryPassHealthCheck.ResponseBody);
    }

    [Fact]
    public void Program_ShouldAnswerTheHealthCheckBeforeTheHttpsRedirect()
    {
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));

        var healthIndex = program.IndexOf("app.Map(BatteryPassHealthCheck.Path", StringComparison.Ordinal);
        var redirectIndex = program.IndexOf("app.UseHttpsRedirection()", StringComparison.Ordinal);

        Assert.True(healthIndex >= 0, "The health check endpoint is not mapped in Program.cs.");
        Assert.True(redirectIndex >= 0, "UseHttpsRedirection is no longer present in Program.cs.");

        // The load balancer polls this path over plain HTTP. If the redirect runs
        // first the check receives a 301, every target is marked unhealthy, and the
        // environment stops serving traffic.
        Assert.True(
            healthIndex < redirectIndex,
            "The health check must be mapped before UseHttpsRedirection, otherwise the load balancer health check gets redirected and all instances are taken out of service.");
    }

    [Fact]
    public void Program_ShouldApplyForwardedHeadersBeforeTheHealthCheckAndRedirect()
    {
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));

        var forwardedIndex = program.IndexOf("app.UseForwardedHeaders()", StringComparison.Ordinal);
        var healthIndex = program.IndexOf("app.Map(BatteryPassHealthCheck.Path", StringComparison.Ordinal);

        Assert.True(forwardedIndex >= 0, "UseForwardedHeaders is no longer present in Program.cs.");
        Assert.True(
            forwardedIndex < healthIndex,
            "Forwarded headers must be applied first so the request scheme is known before anything depends on it.");
    }

    [Fact]
    public void HealthCheckEndpoint_ShouldNotDependOnTheDatabase()
    {
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));

        var healthIndex = program.IndexOf("app.Map(BatteryPassHealthCheck.Path", StringComparison.Ordinal);
        Assert.True(healthIndex >= 0, "The health check endpoint is not mapped in Program.cs.");

        var closingIndex = program.IndexOf("}));", healthIndex, StringComparison.Ordinal);
        Assert.True(closingIndex > healthIndex, "Could not locate the end of the health check branch.");

        var body = program[healthIndex..closingIndex];

        // A health check that queries Mongo turns a database outage into every
        // instance being cycled out of the load balancer, instead of a degraded
        // application that still serves what it can.
        Assert.DoesNotContain("Mongo", body);
        Assert.DoesNotContain("Repository", body);
        Assert.DoesNotContain("await initializer", body);
    }

    [Fact]
    public void Program_ShouldApplyHstsAfterForwardedHeadersSoItActuallyFires()
    {
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));

        var forwardedIndex = program.IndexOf("app.UseForwardedHeaders()", StringComparison.Ordinal);
        var hstsIndex = program.IndexOf("app.UseHsts()", StringComparison.Ordinal);

        Assert.True(forwardedIndex >= 0, "UseForwardedHeaders is no longer present in Program.cs.");
        Assert.True(hstsIndex >= 0, "UseHsts is no longer present in Program.cs.");

        // HSTS only emits its header when the request is recognised as HTTPS. Behind
        // the load balancer nginx reaches Kestrel over plain HTTP, so that is only
        // true after X-Forwarded-Proto has been applied. Registered before
        // UseForwardedHeaders it silently never fires and no header is ever sent.
        Assert.True(
            forwardedIndex < hstsIndex,
            "UseHsts must come after UseForwardedHeaders, otherwise the request still looks like plain HTTP and the HSTS header is never sent.");
    }

    [Fact]
    public void Program_ShouldTellHttpsRedirectionWhichPortToUse()
    {
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));

        // Kestrel is started with plain "--urls http://localhost:5000" because TLS
        // terminates at the load balancer. With no https:// binding and no explicit
        // port, UseHttpsRedirection cannot resolve a target and silently passes every
        // request through - the redirect appears enabled but never fires.
        Assert.Contains("AddHttpsRedirection", program);
        Assert.Contains("options.HttpsPort = HttpsRedirectionDefaults.PublicHttpsPort", program);
        Assert.Equal(443, HttpsRedirectionDefaults.PublicHttpsPort);
    }

    [Fact]
    public void HttpsRedirection_ShouldUseATemporaryRedirectSoTheSettingStaysReversible()
    {
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));

        // A 301 is cached indefinitely by browsers, so turning the redirect back off
        // would not reach anyone who had already visited.
        Assert.Contains("StatusCodes.Status307TemporaryRedirect", program);
        Assert.DoesNotContain("Status301MovedPermanently", program);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("no")]
    [InlineData("1")]
    public void HealthDiagnostics_ShouldStayOffUnlessExplicitlyEnabled(string? configured)
    {
        // The diagnostic body reports internal addresses, so anything other than an
        // explicit "true" must leave it disabled.
        Assert.False(BatteryPassHealthCheck.DiagnosticsEnabled(configured));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData(" true ")]
    public void HealthDiagnostics_ShouldTurnOnForAnExplicitTrue(string configured)
    {
        Assert.True(BatteryPassHealthCheck.DiagnosticsEnabled(configured));
    }

    [Fact]
    public void HealthDiagnostics_ShouldReportBothTheConclusionAndTheRawHeaders()
    {
        var body = BatteryPassHealthCheck.BuildDiagnostics(
            scheme: "http",
            isHttps: false,
            forwardedProto: "https",
            forwardedFor: "203.0.113.5, 10.0.21.221",
            remoteIpAddress: "127.0.0.1");

        // Reporting both sides is the point: a scheme of http alongside an
        // x-forwarded-proto of https pins the fault on the middleware rather than
        // on the proxy chain.
        Assert.Contains("scheme=http", body);
        Assert.Contains("isHttps=False", body);
        Assert.Contains("x-forwarded-proto=https", body);
        Assert.Contains("x-forwarded-for=203.0.113.5, 10.0.21.221", body);
        Assert.Contains("remote-ip=127.0.0.1", body);
    }

    [Fact]
    public void HealthDiagnostics_ShouldMakeMissingHeadersVisibleRatherThanBlank()
    {
        var body = BatteryPassHealthCheck.BuildDiagnostics("http", false, null, "", null);

        Assert.Contains("x-forwarded-proto=(absent)", body);
        Assert.Contains("x-forwarded-for=(absent)", body);
        Assert.Contains("remote-ip=(absent)", body);
    }

    [Theory]
    [InlineData("X-Forwarded-Proto")]
    [InlineData("x-forwarded-for")]
    [InlineData("X-Forwarded-Port")]
    [InlineData("X-Real-IP")]
    [InlineData("Forwarded")]
    public void IsProxyHeader_ShouldRecogniseTheHeadersAProxyRewrites(string headerName)
    {
        Assert.True(BatteryPassHealthCheck.IsProxyHeader(headerName));
    }

    [Theory]
    [InlineData("Host")]
    [InlineData("Authorization")]
    [InlineData("Cookie")]
    [InlineData("User-Agent")]
    public void IsProxyHeader_ShouldNotCaptureUnrelatedOrSensitiveHeaders(string headerName)
    {
        // The snapshot ends up in a response body, so it must never widen to headers
        // carrying credentials or session state.
        Assert.False(BatteryPassHealthCheck.IsProxyHeader(headerName));
    }

    [Fact]
    public void HealthDiagnostics_ShouldReportHeadersAsReceivedBeforeTheyWereConsumed()
    {
        var body = BatteryPassHealthCheck.BuildDiagnostics(
            scheme: "http",
            isHttps: false,
            forwardedProto: "",
            forwardedFor: "",
            remoteIpAddress: "203.0.113.5",
            rawProxyHeaders:
            [
                new("X-Forwarded-Proto", "http"),
                new("X-Forwarded-Port", "443")
            ]);

        // An X-Forwarded-Proto of http alongside a port of 443 is the signature of a
        // proxy overwriting the scheme, which reads identically to "never sent" once
        // the middleware has stripped it.
        Assert.Contains("as received", body);
        Assert.Contains("x-forwarded-proto=http", body);
        Assert.Contains("x-forwarded-port=443", body);
    }

    [Fact]
    public void HealthDiagnostics_ShouldSayPlainlyWhenNoProxyHeadersArrived()
    {
        var body = BatteryPassHealthCheck.BuildDiagnostics("http", false, null, null, "10.0.0.1");

        Assert.Contains("(no proxy headers reached the application)", body);
    }

    [Fact]
    public void HealthEndpoint_ShouldAlwaysReturn200RegardlessOfDiagnostics()
    {
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));

        var healthIndex = program.IndexOf("app.Map(BatteryPassHealthCheck.Path", StringComparison.Ordinal);
        var closingIndex = program.IndexOf("}));", healthIndex, StringComparison.Ordinal);
        var body = program[healthIndex..closingIndex];

        // The status code is set once, before the diagnostics branch, so turning
        // diagnostics on can never change what the load balancer sees.
        Assert.Contains("StatusCodes.Status200OK", body);
        Assert.DoesNotContain("Status500", body);
        Assert.DoesNotContain("Status503", body);
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(parts)}");
    }
}
