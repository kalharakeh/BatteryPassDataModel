using System.Text.RegularExpressions;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BatteryPassWeb.Tests.Platform;

/// <summary>
/// Finding R24: every response carries a content security policy (scripts and styles from the app itself; an inline
/// script only with the response's own nonce, never 'unsafe-inline'), nosniff and a referrer policy; the framing
/// protection stays.
/// </summary>
public sealed class SecurityHeadersTests
{
    private static string Directive(string policy, string name) =>
        policy.Split(';', StringSplitOptions.TrimEntries).Single(part => part.StartsWith(name + " ", StringComparison.Ordinal));

    [Fact]
    public void Policy_ShouldAllowScriptsFromTheAppAndTheResponsesNonceOnly()
    {
        var policy = SecurityHeaders.Policy("abc123");

        var scripts = Directive(policy, "script-src");
        Assert.Equal("script-src 'self' 'nonce-abc123'", scripts);
        Assert.DoesNotContain("unsafe-inline", scripts, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-eval", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("http", policy, StringComparison.Ordinal);
    }

    [Fact]
    public void Policy_ShouldKeepEverythingElseToTheAppItself()
    {
        var policy = SecurityHeaders.Policy("n");

        Assert.Equal("default-src 'self'", Directive(policy, "default-src"));
        Assert.Equal("img-src 'self' data: blob:", Directive(policy, "img-src"));
        Assert.Equal("connect-src 'self'", Directive(policy, "connect-src"));
        Assert.Equal("object-src 'none'", Directive(policy, "object-src"));
        Assert.Equal("base-uri 'self'", Directive(policy, "base-uri"));
        Assert.Equal("form-action 'self'", Directive(policy, "form-action"));
        Assert.Equal("frame-ancestors 'self'", Directive(policy, "frame-ancestors"));
    }

    [Fact]
    public void Policy_ShouldKeepInlineStylesForNow_AndSayWhy()
    {
        // The pages have ten style attributes (a bar's width, a swatch's colour, a grid's columns). A nonce does not cover a
        // style attribute, so style-src keeps 'unsafe-inline' until they move to classes; scripts never get it.
        Assert.Equal("style-src 'self' 'unsafe-inline'", Directive(SecurityHeaders.Policy("n"), "style-src"));
    }

    [Fact]
    public void Apply_ShouldSetTheThreeHeaders_AndKeepTheFramingProtection()
    {
        var context = new DefaultHttpContext();

        SecurityHeaders.Apply(context.Response, "nonce-value");

        var headers = context.Response.Headers;
        Assert.Equal(SecurityHeaders.Policy("nonce-value"), headers.ContentSecurityPolicy.ToString());
        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
        Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"].ToString());
        Assert.Equal("SAMEORIGIN", headers.XFrameOptions.ToString());
    }

    [Fact]
    public void Apply_ShouldLeaveAHeaderThatIsAlreadyThere()
    {
        var context = new DefaultHttpContext();
        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers.XContentTypeOptions = "nosniff";

        SecurityHeaders.Apply(context.Response, "n");

        Assert.Equal("DENY", context.Response.Headers.XFrameOptions.ToString());
        Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions.ToString());
    }

    [Fact]
    public void Nonce_ShouldBeNewForEveryResponse_AndHardToGuess()
    {
        var nonces = Enumerable.Range(0, 50).Select(_ => SecurityHeaders.NewNonce()).ToList();

        Assert.Equal(50, nonces.Distinct().Count());
        Assert.All(nonces, nonce => Assert.Matches("^[A-Za-z0-9+/=]{22,}$", nonce));
    }

    [Fact]
    public void ProgramCs_ShouldAddTheHeadersBeforeAnythingCanAnswer()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));
        var headers = program.IndexOf("app.UseSecurityHeaders();", StringComparison.Ordinal);

        Assert.True(headers > 0);
        Assert.True(headers < program.IndexOf("app.UseExceptionHandler(", StringComparison.Ordinal));
        Assert.True(headers < program.IndexOf("app.UseStaticFiles();", StringComparison.Ordinal));
        Assert.True(headers < program.IndexOf("app.Map(BatteryPassHealthCheck.Path", StringComparison.Ordinal));
    }

    [Fact]
    public void NonceTagHelper_ShouldNonceAnInlineScript_AndNothingElse()
    {
        Assert.Equal("the-nonce", Nonced().GetValue("nonce"));
        Assert.Equal("the-nonce", Nonced(("type", "text/javascript")).GetValue("nonce"));
        Assert.Equal("the-nonce", Nonced(("type", "module")).GetValue("nonce"));
        Assert.Null(Nonced(("src", "/js/x.js")).GetValue("nonce"));
        Assert.Null(Nonced(("type", "application/json")).GetValue("nonce"));
        Assert.Equal("kept", Nonced(("nonce", "kept")).GetValue("nonce"));
    }

    [Fact]
    public void Views_ShouldBeNoncedByTheTagHelper_AndHoldNoOtherInlineCode()
    {
        var imports = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "_ViewImports.cshtml"));
        Assert.Contains("@addTagHelper *, BatteryPassWeb", imports, StringComparison.Ordinal);

        var views = Directory.GetFiles(Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Views", "_ViewStart.cshtml"))!, "*.cshtml", SearchOption.AllDirectories);
        var handler = new Regex(@"\s on(click|change|submit|input|load|error|keydown|keyup|focus|blur|toggle|mouse\w+)\s*=|href\s*=\s*[""']\s*javascript:", RegexOptions.IgnoreCase);
        var styleBlock = new Regex(@"<style[\s>]", RegexOptions.IgnoreCase);
        foreach (var view in views)
        {
            var text = File.ReadAllText(view);
            Assert.False(handler.IsMatch(text), $"{Path.GetFileName(view)} has an inline event handler or a javascript: link, which the policy blocks");
            Assert.False(styleBlock.IsMatch(text), $"{Path.GetFileName(view)} has a style block");
        }
    }

    private static TagHelperAttributeList Nonced(params (string Name, string Value)[] attributes)
    {
        var list = new TagHelperAttributeList(attributes.Select(attribute => new TagHelperAttribute(attribute.Name, attribute.Value)));
        var context = new TagHelperContext("script", list, new Dictionary<object, object>(), Guid.NewGuid().ToString("N"));
        var output = new TagHelperOutput("script", list, (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        var http = new DefaultHttpContext();
        http.Items[SecurityHeaders.NonceItem] = "the-nonce";

        new CspNonceTagHelper { ViewContext = new Microsoft.AspNetCore.Mvc.Rendering.ViewContext { HttpContext = http } }.Process(context, output);
        return output.Attributes;
    }
}

internal static class TagHelperAttributeListExtensions
{
    public static string? GetValue(this TagHelperAttributeList list, string name) => list.TryGetAttribute(name, out var attribute) ? attribute.Value?.ToString() : null;
}
