using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Values;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class DppLinksTests
{
    [Theory]
    [InlineData("https://passport.example.com")]
    [InlineData("https://passport.example.com/")]
    public void Links_ShouldBeBuiltOnTheAppBaseUrl(string baseUrl)
    {
        var links = new DppLinks(Options.Create(new BatteryPassOptions { AppBaseUrl = baseUrl }));

        Assert.Equal("https://passport.example.com/dpp/documents/doc-1", links.DocumentUrl("doc-1"));
        Assert.Equal("https://passport.example.com/dpp/packs/pack-1/accidents", links.AccidentPageUrl("pack-1"));
    }

    [Theory]
    [InlineData("https://passport.example.com/dpp/documents/doc-1", true)]
    [InlineData("https://passport.example.com/dpp/packs/pack-1/accidents", true)]
    [InlineData("https://passport.example.com/other/doc-1", false)]
    [InlineData("https://elsewhere.example.com/dpp/documents/doc-1", false)]
    [InlineData("https://passport.example.com.evil.example/dpp/documents/doc-1", false)]
    public void IsAppUrl_ShouldAcceptOnlyTheAppsOwnDocumentAddresses(string url, bool expected)
    {
        Assert.Equal(expected, DocumentTesting.Links.IsAppUrl(url));
    }

    [Fact]
    public void WithoutAnAppBaseUrl_NoLinkShouldBeIssuedOrAccepted()
    {
        Assert.False(DocumentTesting.NoLinks.IsConfigured);
        Assert.False(DocumentTesting.NoLinks.IsAppUrl("https://passport.example.com/dpp/documents/doc-1"));
        Assert.Equal(DppLinks.NotConfigured, Assert.Throws<InvalidOperationException>(() => DocumentTesting.NoLinks.DocumentUrl("doc-1")).Message);
    }

    [Fact]
    public void AccidentPageLink_ShouldBeAValidValueForAccidents()
    {
        var validator = new AttributeValueValidator(DppStoreTesting.Schemas);

        Assert.Empty(validator.Validate(AttributeCatalog.ByNumber(92), DocumentTesting.Links.AccidentPageLink("pack-1")));
    }

    [Fact]
    public void RestrictedDocuments_ShouldBeTheDocumentAttributesThatArentPublic()
    {
        var documents = AttributeCatalog.FileAttributes.Where(a =>
            DppStoreTesting.Schemas.FullDocument["$defs"]![a.Section]!["properties"]![a.Property]!["$ref"]?.GetValue<string>() == "#/$defs/RelatedResource");

        Assert.Equal(
            documents.Where(a => a.Audience != Audience.Public).Select(a => a.Property).Order(StringComparer.Ordinal),
            DppLinks.RestrictedDocumentProperties.Order(StringComparer.Ordinal));
    }
}
