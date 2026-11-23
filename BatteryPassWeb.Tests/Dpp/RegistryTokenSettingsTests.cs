using BatteryPassWeb.Dpp.RegistryToken;
using static BatteryPassWeb.Tests.Dpp.RegistryTokenTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R3 (decisions 3 and 8): the four EU_REGISTRY_ settings per environment. All four: on; any empty: off. Production
/// doesn't start with only some of them, and outside Development the EU's addresses must be https and the audience the
/// site's origin. The start log says whether the check is on, naming no value but the issuer.
/// </summary>
public sealed class RegistryTokenSettingsTests
{
    private static RegistryTokenSettings Read(params (string Name, string? Value)[] values) =>
        RegistryTokenSettings.Read(name => values.FirstOrDefault(value => value.Name == name).Value);

    private static readonly (string, string?)[] AllFour =
    [
        ("EU_REGISTRY_ISSUER", Issuer), ("EU_REGISTRY_JWKS_URL", JwksUrl), ("EU_REGISTRY_AUDIENCE", Audience), ("EU_REGISTRY_ORG_ID", Organisation),
    ];

    [Fact]
    public void AllFour_ShouldTurnTheCheckOn()
    {
        var settings = Read(AllFour);

        Assert.True(settings.IsOn);
        Assert.Equal(Settings, settings);
        Assert.Null(settings.StartProblem(production: true, development: false));
    }

    [Fact]
    public void NoneOrAnEmptyOne_ShouldLeaveItOff()
    {
        Assert.False(Read().IsOn);
        Assert.False(Read(AllFour.Select(pair => pair.Item1 == "EU_REGISTRY_ORG_ID" ? (pair.Item1, "  ") : pair).ToArray()).IsOn);
        Assert.Null(Read().StartProblem(production: true, development: false)); // a site without the registry starts as before
    }

    [Fact]
    public void OnlySomeInProduction_ShouldStopTheStart_NamingTheMissingOnes()
    {
        var settings = Read(AllFour.Take(2).ToArray());

        var problem = settings.StartProblem(production: true, development: false);

        Assert.NotNull(problem);
        Assert.Contains("EU_REGISTRY_AUDIENCE, EU_REGISTRY_ORG_ID", problem, StringComparison.Ordinal);
        Assert.DoesNotContain(Issuer, problem, StringComparison.Ordinal);
        Assert.Null(settings.StartProblem(production: false, development: true)); // elsewhere: off, with a line in the log
        Assert.Contains("off", settings.StartLine, StringComparison.Ordinal);
        Assert.Contains("EU_REGISTRY_ORG_ID", settings.StartLine, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://auth.acc.product-passport.ec.europa.eu", JwksUrl, Audience)]
    [InlineData(Issuer, "http://auth.acc.product-passport.ec.europa.eu/oauth2/jwks", Audience)]
    [InlineData("auth.acc.product-passport.ec.europa.eu", JwksUrl, Audience)]
    [InlineData(Issuer, JwksUrl, "http://www.sib-scania-eubp.eu")]
    [InlineData(Issuer, JwksUrl, "https://www.sib-scania-eubp.eu/")]
    [InlineData(Issuer, JwksUrl, "https://www.sib-scania-eubp.eu/25S")]
    [InlineData(Issuer, JwksUrl, "https://www.sib-scania-eubp.eu:443")]
    [InlineData(Issuer, JwksUrl, "https://WWW.sib-scania-eubp.eu")]
    public void OutsideDevelopment_TheAddresses_ShouldBeHttps_AndTheAudienceTheOrigin(string issuer, string jwks, string audience)
    {
        var settings = new RegistryTokenSettings(issuer, jwks, audience, Organisation);

        Assert.NotNull(settings.StartProblem(production: true, development: false));
        Assert.NotNull(settings.StartProblem(production: false, development: false)); // a staging site too
    }

    [Fact]
    public void InDevelopment_ALocalCheck_ShouldBeAllowed()
    {
        var settings = new RegistryTokenSettings("http://localhost:5455/test-issuer", "http://localhost:5455/eu-jwks.json", "http://localhost:5455", Organisation);

        Assert.Null(settings.StartProblem(production: false, development: true));
        Assert.NotNull(settings.StartProblem(production: false, development: false));
    }

    [Theory]
    [InlineData("https://auth.product-passport.ec.europa.eu", "https://auth.product-passport.ec.europa.eu/oauth2/jwks")]
    [InlineData("https://auth.acc.product-passport.ec.europa.eu", "https://auth.acc.product-passport.ec.europa.eu/oauth2/jwks")]
    [InlineData("https://auth.acc.product-passport.ec.europa.eu/", "https://AUTH.acc.product-passport.ec.europa.eu/oauth2/jwks")]
    public void TheEusOwnPairs_ShouldStart_WithoutAWarning(string issuer, string jwks)
    {
        var settings = Settings with { Issuer = issuer, JwksUrl = jwks };

        Assert.Null(settings.StartProblem(production: true, development: false));
        Assert.Empty(settings.StartWarnings(development: true));
    }

    [Theory]
    [InlineData("https://auth.product-passport.ec.europa.eu", "https://auth.acc.product-passport.ec.europa.eu/oauth2/jwks")] // production issuer, test keys
    [InlineData("https://auth.acc.product-passport.ec.europa.eu", "https://auth.product-passport.ec.europa.eu/oauth2/jwks")]
    [InlineData("https://auth.product-passport.ec.europa.eu", "https://keys.example.com/oauth2/jwks")]
    [InlineData("https://auth.product-passport.ec.europa.eu", "https://auth.product-passport.ec.europa.eu:8443/oauth2/jwks")]
    public void AKeySetOffTheIssuersSite_ShouldStopTheStart_OrWarnInDevelopment(string issuer, string jwks)
    {
        var settings = Settings with { Issuer = issuer, JwksUrl = jwks };

        var problem = settings.StartProblem(production: true, development: false);
        var warnings = settings.StartWarnings(development: true);

        Assert.NotNull(problem);
        Assert.Contains("EU_REGISTRY_JWKS_URL", problem, StringComparison.Ordinal);
        Assert.NotNull(settings.StartProblem(production: false, development: false)); // a staging site too
        Assert.Null(settings.StartProblem(production: false, development: true));
        var warning = Assert.Single(warnings);
        Assert.Contains(jwks, warning, StringComparison.Ordinal);
        Assert.Contains("EU_REGISTRY_JWKS_URL", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOriginWithItsOwnPort_ShouldBeAllowed()
    {
        Assert.Null((Settings with { Audience = "https://www.sib-scania-eubp.eu:8443" }).StartProblem(production: true, development: false));
    }

    [Fact]
    public void TheStartLine_ShouldNameTheIssuer_AndNoOtherValue()
    {
        var line = Settings.StartLine;

        Assert.Contains("is on", line, StringComparison.Ordinal);
        Assert.Contains(Issuer, line, StringComparison.Ordinal);
        Assert.DoesNotContain(JwksUrl, line, StringComparison.Ordinal);
        Assert.DoesNotContain(Organisation, line, StringComparison.Ordinal);
        Assert.DoesNotContain(Audience, line, StringComparison.Ordinal);
    }
}
