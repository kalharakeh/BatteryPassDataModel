namespace BatteryPassWeb.Models.ViewModels;

public sealed class SearchPageViewModel
{
    public string Query { get; init; } = string.Empty;
    public bool NotFound { get; init; }
}
