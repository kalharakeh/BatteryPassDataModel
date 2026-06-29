using System.Text.RegularExpressions;

namespace BatteryPassWeb.Tests;

public sealed class DemoTelemetrySimulatorScriptTests
{
    [Fact]
    public void DemoTelemetrySimulatorScript_ShouldBeStandaloneAndDocumentSeparatedPcInputs()
    {
        var script = File.ReadAllText(RepoFile("scripts", "demo-telemetry-simulator.ps1"));

        Assert.Contains("[Parameter(Mandatory = $true)]", script);
        Assert.Contains("$BaseUrl", script);
        Assert.Contains("$Token", script);
        Assert.Contains("$BatteryId", script);
        Assert.Contains("$ClusterId", script);
        Assert.Contains("$DiscoverBatteries", script);
        Assert.Contains("Run from another Windows PC", script);
    }

    [Fact]
    public void DemoTelemetrySimulatorScript_ShouldDiscoverResetBatteriesThroughExternalApi()
    {
        var script = File.ReadAllText(RepoFile("scripts", "demo-telemetry-simulator.ps1"));

        Assert.Contains("/api/external/v1/clusters", script);
        Assert.Contains("/api/external/v1/clusters/{0}/batteries", script);
        Assert.Contains("batteryId", script);
        Assert.Contains("currentChargeLevelPct", script);
        Assert.Contains("currentVoltageV", script);
        Assert.Contains("currentCurrentA", script);
        Assert.Contains("currentConsumptionKwh", script);
    }

    [Fact]
    public void DemoTelemetrySimulatorScript_ShouldUseBasicTokenAndTelemetryPointsPayload()
    {
        var script = File.ReadAllText(RepoFile("scripts", "demo-telemetry-simulator.ps1"));

        Assert.Contains("Authorization = \"Basic $encodedToken\"", script);
        Assert.Contains("${Token}:", script);
        Assert.Contains("/api/external/v1/batteries/{0}/telemetry", script);
        Assert.Contains("@{ points = $points }", script);
        Assert.Contains("ConvertTo-Json -Depth 8", script);
    }

    [Fact]
    public void DemoTelemetrySimulatorScript_ShouldDefaultToRateLimitAwareLiveCadence()
    {
        var script = File.ReadAllText(RepoFile("scripts", "demo-telemetry-simulator.ps1"));
        var match = Regex.Match(script, @"\$\s*LiveIntervalSeconds\s*=\s*(\d+)");

        Assert.True(match.Success, "LiveIntervalSeconds default was not found.");
        Assert.True(int.Parse(match.Groups[1].Value) >= 25, "Default live cadence must stay under the 30 writes/min external API limit for eight reset batteries.");
        Assert.Contains("$MaxPointsPerRequest = 200", script);
        Assert.Contains("429", script);
    }

    [Fact]
    public void DemoTelemetrySimulatorScript_ShouldAllowEmptyDiscoveryAccumulatorBeforeBatteriesAreAdded()
    {
        var script = File.ReadAllText(RepoFile("scripts", "demo-telemetry-simulator.ps1"));
        var addTargetBattery = Regex.Match(
            script,
            @"function Add-TargetBattery \{(?<body>.*?)function Get-TargetBatteries",
            RegexOptions.Singleline);

        Assert.True(addTargetBattery.Success, "Add-TargetBattery function was not found.");
        Assert.DoesNotContain("[Parameter(Mandatory = $true)][System.Collections.Generic.List[object]]$Targets", addTargetBattery.Groups["body"].Value);
    }

    private static string RepoFile(params string[] parts)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", Path.Combine(parts)));
    }
}
