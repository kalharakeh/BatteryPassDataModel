using BatteryPassWeb.Configuration;

namespace BatteryPassWeb.Tests;

public sealed class DeploymentSecurityConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("true")]
    [InlineData("yes")]
    public void ParseRequireHttpsRedirection_ShouldDefaultToEnabled(string? value)
    {
        Assert.True(DeploymentSecurityConfiguration.ParseRequireHttpsRedirection(value));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("FALSE")]
    [InlineData("0")]
    [InlineData("no")]
    public void ParseRequireHttpsRedirection_ShouldAllowTemporaryDisableForHttpSmokeTest(string value)
    {
        Assert.False(DeploymentSecurityConfiguration.ParseRequireHttpsRedirection(value));
    }
}
