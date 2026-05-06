namespace BatteryPassWeb.Models.ViewModels;

public sealed class PassportDetailViewModel
{
    public required PassportViewModel Passport { get; init; }
    public string AccessNotice { get; init; } = string.Empty;
    public IReadOnlyList<TelemetryHistoryPointViewModel> TelemetryHistory { get; init; } = [];
}

public sealed class TelemetryHistoryPointViewModel
{
    public string MeasuredAt { get; init; } = string.Empty;
    public double? CurrentConsumptionKwh { get; init; }
    public double? CurrentChargeLevelPct { get; init; }
    public double? CurrentVoltageV { get; init; }
    public double? CurrentCurrentA { get; init; }
}
