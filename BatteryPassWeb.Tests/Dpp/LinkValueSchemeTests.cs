using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Schemas;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>S01 (W21): every value of the kind "link" in the catalogue is an http or https address, and nothing else.</summary>
public sealed class LinkValueSchemeTests
{
    private static readonly OfficialSchemas Schemas = OfficialSchemas.Load();

    private static readonly AttributeValueValidator Validator = new(Schemas);

    private static readonly AttributeForms Forms = new(Schemas);

    /// <summary>The attributes that hold a web address a reader can follow: a public document link, or a plain web link.</summary>
    private static IEnumerable<AttributeDefinition> LinkAttributes() =>
        AttributeCatalog.All.Where(attribute =>
            !attribute.IsExtension
            && (Forms.For(attribute).Kind == FormKind.Link
                || attribute.Property.StartsWith("webLink", StringComparison.Ordinal)
                || (Forms.For(attribute) is { Kind: FormKind.Single } single && single.Inputs.Any(input => input.Kind == InputKind.Url))));

    [Fact]
    public void TheCatalogue_ShouldHaveLinkAttributesToCheck()
    {
        Assert.NotEmpty(LinkAttributes());
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JAVASCRIPT:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("ftp://example.com/doc.pdf")]
    [InlineData("urn:example:doc")]
    public void EveryLinkValue_ShouldRefuseAnAddressThatIsNotHttpOrHttps(string address)
    {
        foreach (var attribute in LinkAttributes())
        {
            JsonNode value = Forms.For(attribute).Kind == FormKind.Link
                ? new JsonObject { ["contentType"] = "application/pdf", ["url"] = address, ["resourceTitle"] = "Document", ["language"] = "en-GB" }
                : JsonValue.Create(address)!;

            Assert.True(Validator.Validate(attribute, value).Count > 0, $"#{attribute.Number} {attribute.Property} accepted {address}");
        }
    }

    [Theory]
    [InlineData("https://[bad")] // passes the schema's pattern, but isn't an address: the validator itself checks it
    public void EveryLinkValue_ShouldBeCheckedByTheValidatorToo_NotOnlyBytheSchemasPattern(string address)
    {
        foreach (var attribute in LinkAttributes())
        {
            JsonNode value = Forms.For(attribute).Kind == FormKind.Link
                ? new JsonObject { ["contentType"] = "application/pdf", ["url"] = address, ["resourceTitle"] = "Document", ["language"] = "en-GB" }
                : JsonValue.Create(address)!;

            Assert.Contains(Validator.Validate(attribute, value), issue => issue.Message.Contains("web address", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void TheExtensionAttributes_ShouldHoldNoLink()
    {
        Assert.NotEmpty(AttributeCatalog.Extensions);
        foreach (var attribute in AttributeCatalog.Extensions)
        {
            var form = Forms.For(attribute);
            Assert.NotEqual(FormKind.Link, form.Kind);
            Assert.DoesNotContain(form.Inputs, input => input.Kind == InputKind.Url);
        }
    }

    [Fact]
    public void EveryLinkValue_ShouldAcceptAnHttpsAddress()
    {
        foreach (var attribute in LinkAttributes())
        {
            JsonNode value = Forms.For(attribute).Kind == FormKind.Link
                ? new JsonObject { ["contentType"] = "application/pdf", ["url"] = "https://www.example.com/doc.pdf", ["resourceTitle"] = "Document", ["language"] = "en-GB" }
                : JsonValue.Create("https://www.example.com/doc.pdf")!;

            Assert.Empty(Validator.Validate(attribute, value));
        }
    }
}

/// <summary>S01 (W21): a link value stored before the rule, or put into the database directly, shows as text, not as a link.</summary>
public sealed class LinkValueShowingTests
{
    private static readonly AttributeForms Forms = new(OfficialSchemas.Load());

    [Theory]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("data:text/html,x", null)]
    [InlineData("https://www.example.com/doc.pdf", "https://www.example.com/doc.pdf")]
    public void ADocumentLink_ShouldBeALinkOnlyForAnHttpOrHttpsAddress(string address, string? href)
    {
        var form = Forms.For(AttributeCatalog.ByNumber(28));
        var value = new JsonObject { ["contentType"] = "application/pdf", ["url"] = address, ["resourceTitle"] = "Manual" };

        var line = Assert.Single(ValueWords.Lines(form, value));

        Assert.Equal("Manual", line.Text);
        Assert.Equal(href, line.Href);
    }

    [Theory]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("https://www.example.com/study", "https://www.example.com/study")]
    public void AWebLink_ShouldBeALinkOnlyForAnHttpOrHttpsAddress(string address, string? href)
    {
        var form = Forms.For(AttributeCatalog.ByNumber(36));

        var line = Assert.Single(ValueWords.Lines(form, JsonValue.Create(address)));

        Assert.Equal(href, line.Href);
    }
}
