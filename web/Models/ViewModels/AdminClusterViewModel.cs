namespace BatteryPassWeb.Models.ViewModels;

public sealed class AdminClusterViewModel
{
    public string SelectedTab { get; init; } = "passports";
    public string PassportsQuery { get; init; } = string.Empty;
    public IReadOnlyList<ClusterViewModel> Clusters { get; init; } = [];
    public IReadOnlyList<PassportSummaryViewModel> Passports { get; init; } = [];
    public IReadOnlyList<UserViewModel> Users { get; init; } = [];
    public IReadOnlyList<ClusterMembershipViewModel> Memberships { get; init; } = [];
}

public sealed class ClusterViewModel
{
    public string ClusterId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed class ClusterMembershipViewModel
{
    public string Email { get; init; } = string.Empty;
    public string ClusterId { get; init; } = string.Empty;
    public string Role { get; init; } = "member";
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed class UserViewModel
{
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = [];
}
