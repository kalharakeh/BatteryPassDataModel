using System.Reflection;
using System.Text.Json;
using BatteryPassWeb.Controllers;

namespace BatteryPassWeb.Tests;

public sealed class ExternalApiTelemetryTemplateTests
{
    [Fact]
    public void TelemetryParser_ShouldAcceptSeriesAndIgnoreMalformedPoints()
    {
        var method = typeof(ExternalApiController).GetMethod(
            "TryParseTelemetryPoints",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        using var document = JsonDocument.Parse("""
        {
          "series": {
            "currentConsumptionKwh": [
              { "measuredAt": "2026-05-20T09:00:00Z", "value": 10.5 },
              { "measuredAt": "broken", "value": 12.1 }
            ],
            "currentVoltageV": [
              { "measuredAt": "2026-05-20T09:05:00Z", "value": 398.4 }
            ]
          }
        }
        """);

        object?[] args = [document.RootElement, null!, ""];
        var success = (bool)method.Invoke(null, args)!;

        Assert.True(success);
        Assert.Contains("ignored", args[2]!.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
