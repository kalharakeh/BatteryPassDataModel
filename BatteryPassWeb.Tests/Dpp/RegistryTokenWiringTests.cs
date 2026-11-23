using System.Reflection;
using System.Security.Claims;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.RegistryToken;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R3 (decision 94): where the EU registry's token is checked, and what doesn't change. The four data addresses check it,
/// the page and the battery ID's address don't; the app checks it before the read limit; a request whose token wasn't
/// checked is refused, never given the public part; a signed-in reader without a token reads as before.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RegistryTokenWiringTests(MongoFixture mongo)
{
    [Fact]
    public void TheFourDataAddresses_ShouldCheckTheToken_ThePageAndTheResolverNot()
    {
        var type = typeof(PassportController);
        foreach (var name in new[] { nameof(PassportController.SummaryData), nameof(PassportController.SummaryJson), nameof(PassportController.VersionData), nameof(PassportController.VersionJson) })
        {
            Assert.NotNull(type.GetMethod(name)!.GetCustomAttribute<RegistryReadableAttribute>());
        }

        Assert.Null(type.GetMethod(nameof(PassportController.Summary))!.GetCustomAttribute<RegistryReadableAttribute>());
        Assert.Null(type.GetMethod(nameof(PassportController.Detail))!.GetCustomAttribute<RegistryReadableAttribute>());
        Assert.Null(typeof(ItemIdController).GetMethod(nameof(ItemIdController.Item))!.GetCustomAttribute<RegistryReadableAttribute>());
    }

    [Theory]
    [InlineData("Bearer abc", null, null)]
    [InlineData("Basic dXNlcjpwYXNz", null, null)]
    [InlineData(null, "?access_token=abc", null)]
    [InlineData(null, null, "access_token=abc")]
    public async Task AnUncheckedCredential_ShouldBe401_NeverThePublicPart(string? authorization, string? query, string? cookie)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var controller = PublicPassportDataTests.Controller(rig);
        var request = controller.ControllerContext.HttpContext.Request;
        if (authorization is not null)
        {
            request.Headers.Authorization = authorization;
        }

        if (query is not null)
        {
            request.QueryString = new QueryString(query);
        }

        if (cookie is not null)
        {
            request.Headers.Cookie = cookie;
        }

        var answer = await controller.SummaryJson(Uri.EscapeDataString(PassportPagesTests.Identifier(rig)), CancellationToken.None);

        var refusal = Assert.IsType<ContentResult>(answer);
        Assert.Equal(StatusCodes.Status401Unauthorized, refusal.StatusCode);
        Assert.Equal(RegistryTokenAnswers.ProblemType, refusal.ContentType);
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal(RegistryTokenRequest.Vary, controller.Response.Headers.Vary.ToString());
    }

    [Fact]
    public async Task ASignedInReaderWithoutAToken_ShouldReadThePublicPartAsBefore()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var id = Uri.EscapeDataString(PassportPagesTests.Identifier(rig));
        var signedIn = PublicPassportDataTests.Controller(rig);
        signedIn.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "team@example.com"), new Claim(ClaimTypes.Email, "team@example.com"), new Claim(ClaimTypes.Role, "admin")],
            Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme));

        var asTeam = await PublicPassportDataTests.DataAsync(signedIn.SummaryJson(id, CancellationToken.None));
        var anonymous = await PublicPassportDataTests.DataAsync(PublicPassportDataTests.Controller(rig).SummaryJson(id, CancellationToken.None));

        Assert.Equal(anonymous.ToJsonString(), asTeam.ToJsonString());
        Assert.Equal("no-cache, no-store", signedIn.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public void ProgramCs_ShouldCheckTheTokenAfterTheCookiesAndBeforeTheReadLimit()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));

        var authentication = program.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
        var token = program.IndexOf("app.UseRegistryToken();", StringComparison.Ordinal);
        var limiter = program.IndexOf("app.UseRateLimiter();", StringComparison.Ordinal);
        Assert.True(authentication >= 0 && token > authentication && limiter > token);
        Assert.Contains(".AddRegistryToken();", program, StringComparison.Ordinal);
        Assert.Contains("builder.Services.AddRegistryToken(registryToken);", program, StringComparison.Ordinal);
        Assert.Contains(".AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)", program, StringComparison.Ordinal); // cookies stay the default
        // The start check itself is tested on a real start of the app: RegistryTokenStartTests.
    }

    [Fact]
    public void TheEuRegistry_ShouldBeNoAccountsRole()
    {
        Assert.DoesNotContain(ReaderRole.EuRegistry, DppReaders.AppRoles.Values);
    }
}
