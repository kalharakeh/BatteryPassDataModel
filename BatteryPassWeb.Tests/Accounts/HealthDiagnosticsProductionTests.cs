using BatteryPassWeb.Configuration;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>Finding W20: HEALTH_DIAGNOSTICS can't be on in Production, where the anonymous /health page would show proxy headers and addresses.</summary>
public sealed class HealthDiagnosticsProductionTests
{
    [Theory]
    [InlineData(true, "true", true)]
    [InlineData(true, " TRUE ", true)]
    [InlineData(true, "false", false)]
    [InlineData(true, null, false)]
    [InlineData(true, "", false)]
    [InlineData(false, "true", false)]
    public void Production_ShouldRefuseToStart_WithHealthDiagnosticsOn(bool isProduction, string? setting, bool refused)
    {
        var message = BatteryPassHealthCheck.ProductionRefusal(isProduction, setting);

        Assert.Equal(refused, message is not null);
        if (refused)
        {
            Assert.Contains("HEALTH_DIAGNOSTICS", message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ProgramCs_ShouldCheckHealthDiagnostics_BeforeBuildingTheApp()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));

        Assert.True(program.IndexOf("BatteryPassHealthCheck.ProductionRefusal(", StringComparison.Ordinal)
            is var at and >= 0
            && at < program.IndexOf("var app = builder.Build();", StringComparison.Ordinal));
    }
}
