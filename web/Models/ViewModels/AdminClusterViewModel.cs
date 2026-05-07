using BatteryPassWeb.Models.Trust;

namespace BatteryPassWeb.Models.ViewModels;

public sealed class AdminClusterViewModel
{
    public string SelectedTab { get; init; } = "passports";
    public string PassportsQuery { get; init; } = string.Empty;
    public IReadOnlyList<ClusterViewModel> Clusters { get; init; } = [];
    public IReadOnlyList<PassportSummaryViewModel> Passports { get; init; } = [];
    public IReadOnlyList<UserViewModel> Users { get; init; } = [];
    public IReadOnlyList<ClusterMembershipViewModel> Memberships { get; init; } = [];
    public IReadOnlyList<ApiTokenViewModel> ApiTokens { get; init; } = [];
    public IReadOnlyList<BatterySecretViewModel> BatterySecrets { get; init; } = [];
    public DataCompletionPolicySnapshot DataRequirements { get; init; } = new();
    public string SamplePassportId { get; init; } = string.Empty;
    public string SampleReadToken { get; init; } = string.Empty;
    public string SampleReadWriteToken { get; init; } = string.Empty;
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public string GeneratedCredential { get; init; } = string.Empty;
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

public sealed class ApiTokenViewModel
{
    public string TokenId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string AccessMode { get; init; } = "read";
    public bool GlobalAccess { get; init; }
    public bool AllowUnassigned { get; init; }
    public bool IsActive { get; init; } = true;
    public bool IsSample { get; init; }
    public string ClusterIdsLabel { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
    public string LastUsedAt { get; init; } = string.Empty;
}

public sealed class BatterySecretViewModel
{
    public string PassportId { get; init; } = string.Empty;
    public string ClusterId { get; init; } = string.Empty;
    public string ClusterLabel { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}
