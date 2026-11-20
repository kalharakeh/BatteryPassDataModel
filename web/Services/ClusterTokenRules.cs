namespace BatteryPassWeb.Services;

/// <summary>
/// Finding W9 (decisions 29 and 33): a token for clusters reads, or reads and sends live values; creating and publishing
/// batteries is for a token for all batteries. Older tokens may have more access written on them: they are listed with
/// what they can still do and the team may replace them; nothing is changed or deleted by itself.
/// </summary>
public static class ClusterTokenRules
{
    /// <summary>What the team's form says when "Passport lifecycle" or "Full access" is chosen for clusters.</summary>
    public const string OnlyForAllBatteries = "Creating and publishing batteries is only for a token for all batteries. For clusters, choose Read or Read + write.";

    /// <summary>Whether the access written on the token goes further than a cluster token may (it signs, for clusters only).</summary>
    public static bool HasMoreThanAClusterToken(string? accessMode, bool globalAccess) =>
        !globalAccess
        && (string.Equals(accessMode, "sign", StringComparison.OrdinalIgnoreCase)
            || string.Equals(accessMode, "readWriteSign", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// What such a token can still do, as the tokens page says it: in <paramref name="word"/>, "cluster" on the team's page,
    /// "customer" on a customer admin's (decision 55; review 1 of N1, point 3).
    /// </summary>
    public static string StillDoes(string? accessMode, string word = "cluster") =>
        string.Equals(accessMode, "readWriteSign", StringComparison.OrdinalIgnoreCase)
            ? $"Made with more access than a {word} token has. It still reads and sends live values for its {word}s; it can't create or publish batteries."
            : $"Made with more access than a {word} token has. It still only reads its {word}s' batteries; it can't create or publish batteries.";

    /// <summary>The access a replacement gets: what the old token could really do.</summary>
    public static ExternalTokenAccessMode ReplacementMode(string? accessMode) =>
        string.Equals(accessMode, "readWriteSign", StringComparison.OrdinalIgnoreCase) ? ExternalTokenAccessMode.ReadWrite : ExternalTokenAccessMode.Read;

    /// <summary>The replacement's name: a name made per cluster ends in the new kind; any other name stays.</summary>
    public static string ReplacementName(string name) =>
        name.EndsWith(" - readwritesign", StringComparison.OrdinalIgnoreCase) ? name[..^" - readwritesign".Length] + " - readwrite"
        : name.EndsWith("-readwritesign", StringComparison.OrdinalIgnoreCase) ? name[..^"-readwritesign".Length] + "-readwrite"
        : name;
}
