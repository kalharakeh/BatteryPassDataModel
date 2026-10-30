namespace BatteryPassWeb.Models.ViewModels;

public sealed class ExternalApiHelpViewModel
{
    public string BasePath { get; init; } = "/api/external/v1";
    public string PublicBaseUrl { get; init; } = string.Empty;

    /// <summary>A published battery's ID (#6), to try the reading calls on; empty when none is published yet.</summary>
    public string ExampleBatteryId { get; init; } = string.Empty;
}
