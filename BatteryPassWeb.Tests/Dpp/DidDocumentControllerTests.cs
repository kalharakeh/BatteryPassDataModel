using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Finding R23: the app publishes its public keys at /.well-known/did.json, for anyone, public keys only.</summary>
public sealed class DidDocumentControllerTests
{
    [Fact]
    public void DidJson_ShouldShowTheKeyWithTheProofsKeyId()
    {
        using var keys = new DemoSigningKeyService(ECDsa.Create(ECCurve.NamedCurves.nistP256), "https://www.sib-scania-eubp.eu");
        var controller = new DidDocumentController(keys) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var result = Assert.IsType<ContentResult>(controller.Get());
        var document = JsonNode.Parse(result.Content!)!;

        Assert.Equal("application/did+json", result.ContentType);
        Assert.Equal("did:web:www.sib-scania-eubp.eu", document["id"]!.GetValue<string>());
        Assert.Equal(keys.VerificationMethod, document["verificationMethod"]![0]!["id"]!.GetValue<string>());
        Assert.DoesNotContain("\"d\"", result.Content!, StringComparison.Ordinal);
    }

    [Fact]
    public void DidJson_ShouldBeAtTheStandardPlace_ForAnyone()
    {
        var method = typeof(DidDocumentController).GetMethod(nameof(DidDocumentController.Get))!;

        Assert.Equal("/.well-known/did.json", Assert.Single(method.GetCustomAttributes(typeof(HttpGetAttribute), false).Cast<HttpGetAttribute>()).Template);
        Assert.NotEmpty(typeof(DidDocumentController).GetCustomAttributes(typeof(AllowAnonymousAttribute), false));
    }
}
