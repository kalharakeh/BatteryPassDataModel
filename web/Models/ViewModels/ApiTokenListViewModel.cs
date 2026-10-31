namespace BatteryPassWeb.Models.ViewModels;

/// <summary>The token list shared by the team's and the cluster admins' API token pages (step 3e): the tokens, the address
/// their actions post to, whether a token can be switched off and on (the team's page only), and what an empty list says.</summary>
public sealed record ApiTokenListViewModel(IReadOnlyList<ApiTokenViewModel> Tokens, string ActionBase, bool CanSetActive, string EmptyText);
