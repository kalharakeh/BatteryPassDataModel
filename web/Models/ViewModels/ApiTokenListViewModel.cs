namespace BatteryPassWeb.Models.ViewModels;

/// <summary>The token list shared by the team's and the cluster admins' API token pages (step 3e): the tokens, the address
/// their actions post to, whether a token can be switched off and on and deleted (the team's page only: a cluster admin's
/// "delete" only switches a token off), and what an empty list says.</summary>
public sealed record ApiTokenListViewModel(IReadOnlyList<ApiTokenViewModel> Tokens, string ActionBase, bool CanSetActive, bool CanDelete, string EmptyText);
