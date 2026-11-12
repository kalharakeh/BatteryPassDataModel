using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The issuer's public keys at the standard place for its name (finding R23): <c>did:web:www.example.eu</c> is read from
/// <c>https://www.example.eu/.well-known/did.json</c>. Anyone may read it; it holds public keys only.
/// </summary>
[AllowAnonymous]
public sealed class DidDocumentController(DemoSigningKeyService keys) : Controller
{
    [HttpGet(DemoSigningKeyService.DidDocumentPath)]
    public IActionResult Get()
    {
        Response.Headers[HeaderNames.CacheControl] = "public, max-age=300";
        return Content(keys.DidDocument().ToJsonString(), "application/did+json");
    }
}
