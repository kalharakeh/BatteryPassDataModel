using System.Security.Claims;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using static BatteryPassWeb.Tests.Dpp.SmallLetterSerialTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// S1 round two, point 3: text with a control character is never an ID. A NUL that reached a regular expression gave a
/// 500 (<c>/dpp/a%2500</c>); every look-up now finds nothing for it, so every route answers "not found".
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ControlCharacterAddressTests(MongoFixture mongo)
{
    public static TheoryData<string> Texts() => new("a\0", "UN778474481-VPC8C05s-00001\0", "\0", "a\u0001b", "VPC8C05s-00001\n");

    [Fact]
    public void TextWithAControlCharacter_ShouldNeverBeLookedUp()
    {
        Assert.False(IdentifierForms.IsLookup("a\0"));
        Assert.False(IdentifierForms.IsLookup("a\tb"));
        Assert.True(IdentifierForms.IsLookup("UN778474481-VPC8C05s-00001"));
        Assert.True(IdentifierForms.IsLookup("urn:x-battery-pass:TEST-0f"));
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public async Task EveryLookUp_ShouldFindNothing_WithoutThrowing(string text)
    {
        var (rig, pool, _) = await SeededAsync(mongo);
        using var __ = rig;

        Assert.Null(await rig.Store.FindRemovedAsync(text));
        Assert.Null(await rig.Store.FindPackByUniqueProductIdentifierAsync(text));
        Assert.Null(await rig.Store.GetVersionByPassportIdAsync(text));
        Assert.Empty(await rig.Store.FindPacksBySerialNumberAsync(text));
        Assert.False(await rig.Store.SerialNumberTakenAsync(text));
        Assert.Null(await pool.FindUnpublishedAsync(text));
        Assert.Null(await rig.Services.GetRequiredService<PassportPages>().FindAsync(text));
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public async Task ThePublicRoutes_ShouldAnswerNotFound(string text)
    {
        var (rig, pool, _) = await SeededAsync(mongo);
        using var __ = rig;
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()), RequestServices = rig.Services };
        var pages = new PassportController(rig.Services.GetRequiredService<DppReaders>(), rig.Services.GetRequiredService<PassportPages>())
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempDataKept()),
        };
        var api = new PassportsApiController(rig.Services.GetRequiredService<DppReaders>(), rig.Services.GetRequiredService<BatteryPages>(), rig.Services.GetRequiredService<BatteryPassWeb.Dpp.Api.PassportApi>(), rig.Services.GetRequiredService<PassportPages>())
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
        var typed = Uri.EscapeDataString(text); // as an address carries it: decoded once more by the routes

        Assert.Equal(404, Assert.IsType<ViewResult>(await pages.Summary(typed, CancellationToken.None, pool)).StatusCode);
        Assert.IsType<NotFoundObjectResult>(await pages.SummaryJson(typed, CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await api.Verify(typed, CancellationToken.None));
        Assert.IsType<NotFoundResult>(new ItemIdController().Item(text));
    }
}
