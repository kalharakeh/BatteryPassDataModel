using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Models.ViewModels;

public sealed class AdminClusterViewModel
{
    public string SelectedTab { get; init; } = "clusters";
    public string SelectedCredentialTab { get; init; } = "api-tokens";
    public IReadOnlyList<ClusterViewModel> Clusters { get; init; } = [];
    public IReadOnlyList<UserViewModel> Users { get; init; } = [];
    public IReadOnlyList<ClusterMembershipViewModel> Memberships { get; init; } = [];
    public IReadOnlyList<ApiTokenViewModel> ApiTokens { get; init; } = [];
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public string GeneratedCredential { get; init; } = string.Empty;
    public IReadOnlyList<DemoToken> GeneratedCredentials { get; init; } = [];
    public bool HasDemoData { get; init; }
    public bool DemoAccountsPossible { get; init; }
    public int DemoBatteryCount { get; init; }
    public int SessionTimeoutMinutes { get; init; } = 180;
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
    public IReadOnlyList<string> ClusterIds { get; init; } = [];
    public IReadOnlyList<string> ClusterScopeNames { get; init; } = [];
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
    public string LastUsedAt { get; init; } = string.Empty;
}
