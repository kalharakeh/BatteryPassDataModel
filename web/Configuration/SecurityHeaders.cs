using System.Security.Cryptography;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BatteryPassWeb.Configuration;

/// <summary>
/// The security headers of every response (finding R24): a content security policy, "nosniff" and a referrer policy; the
/// framing protection stays. The policy lets the app's own scripts and styles run and nothing from another host. An inline
/// script runs only with the nonce made for that response (see <see cref="CspNonceTagHelper"/>), so a script that someone
/// slips into a page is not run.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>Where the response's nonce is kept while the response is made.</summary>
    public const string NonceItem = "bp.csp-nonce";

    public const string ReferrerPolicy = "strict-origin-when-cross-origin";

    /// <summary>
    /// 'unsafe-inline' stays in style-src only: ten style attributes remain in the pages (a bar's width, a swatch's colour,
    /// the columns of a list), and a nonce does not cover a style attribute. Scripts never get it.
    /// </summary>
    public static string Policy(string nonce) => string.Join("; ",
        "default-src 'self'",
        $"script-src 'self' 'nonce-{nonce}'",
        "style-src 'self' 'unsafe-inline'",
        "img-src 'self' data: blob:",
        "font-src 'self'",
        "connect-src 'self'",
        "object-src 'none'",
        "base-uri 'self'",
        "form-action 'self'",
        "frame-ancestors 'self'");

    public static string NewNonce() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

    /// <summary>Sets the headers a response has none of yet (the framing protection and "nosniff" may be there already).</summary>
    public static void Apply(HttpResponse response, string nonce)
    {
        var headers = response.Headers;
        headers.ContentSecurityPolicy = Policy(nonce);
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = ReferrerPolicy;
        if (!headers.ContainsKey("X-Frame-Options"))
        {
            headers.XFrameOptions = "SAMEORIGIN";
        }
    }

    /// <summary>
    /// First in the pipeline, and written when the response starts, so that an error page, a static file, the health check
    /// and a redirect carry them too (the exception handler clears the headers it finds when it takes over).
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            var nonce = NewNonce();
            context.Items[NonceItem] = nonce;
            context.Response.OnStarting(() =>
            {
                Apply(context.Response, nonce);
                return Task.CompletedTask;
            });
            return next(context);
        });
}

/// <summary>Gives every inline script of a view the nonce of the response it is in. Scripts with a src and data blocks need none.</summary>
[HtmlTargetElement("script")]
public sealed class CspNonceTagHelper : TagHelper
{
    [HtmlAttributeNotBound]
    [Microsoft.AspNetCore.Mvc.ViewFeatures.ViewContext]
    public Microsoft.AspNetCore.Mvc.Rendering.ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (output.Attributes.ContainsName("src") || output.Attributes.ContainsName("nonce"))
        {
            return;
        }

        var type = output.Attributes.TryGetAttribute("type", out var attribute) ? attribute.Value?.ToString() : null;
        if (!string.IsNullOrWhiteSpace(type) && !type.Equals("text/javascript", StringComparison.OrdinalIgnoreCase) && !type.Equals("module", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (ViewContext.HttpContext.Items[SecurityHeaders.NonceItem] is string nonce)
        {
            output.Attributes.SetAttribute("nonce", nonce);
        }
    }
}
