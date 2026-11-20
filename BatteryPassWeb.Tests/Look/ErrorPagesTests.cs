using System.Net;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Finding U04 (and the code sweep's W17): an unknown address gets the app's own "not found" page with the search box,
/// status 404; an unexpected error gets a page that says something went wrong, without any detail, status 500; both in
/// the app's layout. The API's own answers and an unknown passport ID's page stay as they were.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ErrorPagesTests(MongoFixture mongo)
{
    [Theory]
    [InlineData("/no-such-page")]
    [InlineData("/admin/no-such-page")]
    [InlineData("/registry/no/such/page")]
    public async Task AnUnknownAddress_ShouldGetTheNotFoundPage(string path)
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext());

        var (status, body, _) = await site.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Contains("<h1>Page not found</h1>", body, StringComparison.Ordinal);
        Assert.Contains("<form action=\"/search\"", body, StringComparison.Ordinal);
        Assert.Contains("<header class=\"bp-topbar", body, StringComparison.Ordinal);
        Assert.Contains("<title>Page not found - Battery Passport</title>", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownAddress_ShouldShowTheBar_ForWhoeverIsSignedIn()
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext());

        var (status, body, _) = await site.GetAsync("/no-such-page", claims: "pat@example.test|Pat Person|member");

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Contains(">Pat Person</span>", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/api/no-such-call")]
    [InlineData("/api/external/v1/no-such-call")]
    public async Task TheApi_ShouldKeepItsOwnPlainAnswers(string path)
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext());

        var (status, body, _) = await site.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.DoesNotContain("<html", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownPassportId_ShouldKeepItsOwnPage()
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext());

        var (_, body, _) = await site.GetAsync("/dpp/NO-SUCH-BATTERY-ID");

        Assert.DoesNotContain("Page not found", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnError_ShouldGetTheErrorPage_WithoutAnyDetail()
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext(), throwOn: "/forced-error");

        var (status, body, _) = await site.GetAsync("/forced-error");

        Assert.Equal(HttpStatusCode.InternalServerError, status);
        Assert.Contains("<h1>Something went wrong</h1>", body, StringComparison.Ordinal);
        Assert.Contains("<header class=\"bp-topbar", body, StringComparison.Ordinal);
        Assert.DoesNotContain("SHELLSECRETDETAIL", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheErrorPage_ShouldStillDraw_WhenTheDatabaseIsDown()
    {
        // A database nobody answers: every page that reads it fails, and the error page must not fail with it.
        var down = new MongoContext(Options.Create(new BatteryPassOptions
        {
            MongoDbUri = "mongodb://127.0.0.1:9/?serverSelectionTimeoutMS=300&connectTimeoutMS=300",
            MongoDbName = "nobody_home",
        }));
        await using var site = await ShellSite.StartAsync(down);

        var (status, body, _) = await site.GetAsync("/registry", claims: "pat@example.test|Pat Person|clusterAdmin");

        Assert.Equal(HttpStatusCode.InternalServerError, status);
        Assert.Contains("<h1>Something went wrong</h1>", body, StringComparison.Ordinal);
        Assert.Contains(">Pat Person</span>", body, StringComparison.Ordinal); // the bar, from the session
        Assert.DoesNotContain("TimeoutException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1:9", body, StringComparison.Ordinal);
    }

    /// <summary>Review 1, point 4: a known address asked with the wrong method keeps its 405, as before the "not found" page.</summary>
    [Theory]
    [InlineData("/login/logout")]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/logout")]
    public async Task AKnownAddressWithTheWrongMethod_ShouldKeepIts405(string path)
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext());

        var (status, body, _) = await site.GetAsync(path);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, status);
        Assert.DoesNotContain("Page not found", body, StringComparison.Ordinal);
    }

    /// <summary>Review 1, point 4: an error inside an API call answers as the API does, never with the HTML page.</summary>
    [Fact]
    public async Task AnErrorInAnApiCall_ShouldAnswerAPlain500()
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext(), throwOn: "/api/external/v1/forced-error");

        var (status, body, _) = await site.GetAsync("/api/external/v1/forced-error");

        Assert.Equal(HttpStatusCode.InternalServerError, status);
        Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SHELLSECRETDETAIL", body, StringComparison.Ordinal);
        Assert.Contains("\"error\"", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/css/no-such-file.css")]
    [InlineData("/images/no-such-picture.png")]
    public async Task AMissingFile_ShouldKeepItsPlain404(string path)
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext());

        var (status, body, _) = await site.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheApp_ShouldUseTheSameWiring()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));

        Assert.Contains("app.UseErrorPages();", program, StringComparison.Ordinal);
        Assert.Contains("app.MapNotFoundPage();", program, StringComparison.Ordinal);
        Assert.DoesNotContain("app.UseExceptionHandler(\"/error\");", program, StringComparison.Ordinal);
    }
}
