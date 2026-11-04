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

    // Step 6a: without MONGODB_DB the app falls back to battery_pass_demo, the live app's database ("Where it runs").
    // In Production the name must be given; Development keeps today's fallback.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProductionWithoutADatabaseName_ShouldRefuseToStart(string? mongoDb)
    {
        var message = DeploymentSecurityConfiguration.MissingDatabaseName(isProduction: true, mongoDb);

        Assert.NotNull(message);
        Assert.Contains("MONGODB_DB", message, StringComparison.Ordinal);
        Assert.Contains("battery_pass_demo", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, "battery_pass_v3")]
    [InlineData(true, "battery_pass_demo")]
    [InlineData(false, null)]
    [InlineData(false, "")]
    public void AGivenDatabaseName_OrDevelopment_ShouldStart(bool isProduction, string? mongoDb) =>
        Assert.Null(DeploymentSecurityConfiguration.MissingDatabaseName(isProduction, mongoDb));

    [Fact]
    public void TheAppsStart_ShouldCheckTheDatabaseName()
    {
        var program = File.ReadAllText(BatteryPassWeb.Tests.Dpp.DppTestFiles.RepoFile("web", "Program.cs")).Replace("\r\n", "\n");

        Assert.Contains(
            "if (DeploymentSecurityConfiguration.MissingDatabaseName(builder.Environment.IsProduction(), Environment.GetEnvironmentVariable(\"MONGODB_DB\")) is { } missingDatabase)\n{\n    throw new InvalidOperationException(missingDatabase);\n}",
            program,
            StringComparison.Ordinal);
    }
}
