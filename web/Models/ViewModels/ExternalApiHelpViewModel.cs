namespace BatteryPassWeb.Models.ViewModels;

public sealed class ExternalApiHelpViewModel
{
    public string BasePath { get; init; } = "/api/external/v1";
    public string SampleBatteryId { get; init; } = string.Empty;
    public string SamplePassportId { get; init; } = string.Empty;
    public string SampleReadToken { get; init; } = string.Empty;
    public string SampleReadWriteToken { get; init; } = string.Empty;
    public string SampleLifecycleToken { get; init; } = string.Empty;
}
