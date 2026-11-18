using System.Security.Claims;

namespace BatteryPassWeb.Services;

/// <summary>One main link of the top bar (finding L04).</summary>
/// <param name="Current">The link of the page being shown: underlined, and marked for screen readers.</param>
/// <param name="CountsWaiting">The Registry's link, which shows the hand-overs waiting for a customer's admin.</param>
public sealed record TopBarLink(string Text, string Href, bool Current, bool CountsWaiting = false);

/// <summary>The signed-in account as the account menu shows it (finding L04).</summary>
/// <param name="Customer">The customers the account belongs to, for a customer's people only; null for everyone else.</param>
public sealed record TopBarAccount(string Name, string Initials, string Email, string Role, string? Customer);

/// <summary>
/// The one top bar (finding L04, the owner's approved picture of 13 Nov 2026): the app's name, the main links by who is
/// signed in, and the account as initials and a name that opens a menu.
/// </summary>
public static class TopBar
{
    /// <summary>The app's one name (finding W03).</summary>
    public const string AppName = "Battery Passport";

    /// <summary>
    /// The main links: the team "To do", "Admin", "Registry", "Help"; a customer's admin "Registry", "Admin", "Help"; other
    /// readers "Registry", "Help"; a visitor "Help" (and "Log in", which the bar shows apart).
    /// </summary>
    public static IReadOnlyList<TopBarLink> Links(ClaimsPrincipal user, string? path)
    {
        ArgumentNullException.ThrowIfNull(user);
        var here = path ?? string.Empty;
        var links = new List<TopBarLink>();
        if (user.Identity?.IsAuthenticated != true)
        {
            links.Add(new TopBarLink("Help", "/help", Under(here, "/help")));
            return links;
        }

        var team = AccessControlService.IsAdmin(user);
        var registry = new TopBarLink("Registry", "/registry", Under(here, "/registry"), CountsWaiting: true);
        if (team)
        {
            links.Add(new TopBarLink("To do", "/admin/start", Under(here, "/admin/start")));
            links.Add(new TopBarLink("Admin", "/admin/batteries", Under(here, "/admin") && !Under(here, "/admin/start")));
            links.Add(registry);
        }
        else if (user.IsInRole(AccessControlService.RoleClusterAdmin))
        {
            links.Add(registry);
            links.Add(new TopBarLink("Admin", "/cluster-admin", Under(here, "/cluster-admin")));
        }
        else
        {
            links.Add(registry);
        }

        links.Add(new TopBarLink("Help", "/help", Under(here, "/help")));
        return links;
    }

    /// <summary>The role as the account menu names it: "Customer admin" and "Customer user" for a customer's people (W07).</summary>
    public static string RoleWord(ClaimsPrincipal user) => AccessControlService.DisplayRoleLabel(user);

    /// <summary>The first letters of the name's first two words, in capitals; "?" for no name.</summary>
    public static string Initials(string? name)
    {
        var text = (name ?? string.Empty).Trim();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "?";
        }

        // An e-mail address as the name gives one letter.
        return text.Contains('@', StringComparison.Ordinal)
            ? char.ToUpperInvariant(text[0]).ToString()
            : string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])));
    }

    /// <summary>
    /// The account menu's lines: the name (as stored, or the session's), the e-mail address, the role and, for a
    /// customer's people, their customers. A database error falls back to what the session carries, so the bar still
    /// draws on the error page.
    /// </summary>
    public static async Task<TopBarAccount?> AccountAsync(ClaimsPrincipal user, ClusterRepository clusters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(clusters);
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var email = AccessControlService.CurrentEmail(user);
        var name = user.FindFirstValue(ClaimTypes.Name) ?? email;
        string? customer = null;
        try
        {
            if (await clusters.GetUserByEmailAsync(email, cancellationToken) is { } stored
                && BsonHelpers.GetString(stored, "name") is { Length: > 0 } storedName)
            {
                name = storedName;
            }

            if (!AccessControlService.HasAppWideRole(user.Claims.Where(claim => claim.Type == ClaimTypes.Role).Select(claim => claim.Value)))
            {
                var memberships = await clusters.GetClusterMembershipsForUserAsync(email, cancellationToken);
                var names = (await clusters.ListClustersAsync(cancellationToken))
                    .Select(cluster => (Id: BsonHelpers.GetString(cluster, "clusterId"), Name: BsonHelpers.GetString(cluster, "name")))
                    .Where(cluster => cluster.Id.Length > 0)
                    .GroupBy(cluster => cluster.Id, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);
                var customers = memberships
                    .Select(membership => BsonHelpers.GetString(membership, "clusterId"))
                    .Where(id => id.Length > 0)
                    .Select(id => names.TryGetValue(id, out var found) && found.Length > 0 ? found : id)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                // A customer's person with no customer yet says so (finding W04).
                customer = customers.Length > 0 ? string.Join(", ", customers) : "none yet";
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The session's own name; no customer line (the error page draws the bar without the database).
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = email;
        }

        return new TopBarAccount(name, Initials(name), email, RoleWord(user), customer);
    }

    private static bool Under(string path, string prefix) =>
        path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
}
