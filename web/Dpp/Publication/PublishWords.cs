using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Dpp.Publication;

/// <summary>
/// What a publish attempt did, in the team's words (round two, A3 and A4): one sentence per passport, and what holds a
/// passport back, with who can add each missing value.
/// </summary>
public static partial class PublishWords
{
    /// <summary>A blocking line as a short item: "#83 Number of full … (needed because …)" or the model's questions.</summary>
    public static string Item(string blocking)
    {
        ArgumentNullException.ThrowIfNull(blocking);
        if (blocking == PassportNeeds.QuestionsNotAnswered)
        {
            return "the answers to its model's three questions";
        }

        return Missing().Match(blocking) is { Success: true } missing
            ? missing.Groups[3].Success ? $"{missing.Groups[1].Value} ({missing.Groups[3].Value})" : missing.Groups[1].Value
            : blocking;
    }

    /// <summary>The attribute a blocking line is about, e.g. #83 for "#83 … is missing"; null for the others.</summary>
    public static AttributeDefinition? Attribute(string blocking) =>
        Label().Match(blocking ?? string.Empty) is { Success: true } match
            ? AttributeCatalog.All.FirstOrDefault(attribute => attribute.Label == match.Groups[1].Value)
            : null;

    /// <summary>A value the battery's cluster admin can add on the battery's page, under Live values (#63, #83, #87, E1–E4).</summary>
    public static bool ClusterAdminAdds(AttributeDefinition? attribute) =>
        attribute is not null && attribute.Change == ChangeRule.Live && attribute.Editors.HasFlag(AttributeEditors.BatteryClusterAdmins);

    /// <summary>
    /// Why a passport can't get a new version, as a refusal's reason (A4): for the team, what to fill in; for a cluster
    /// admin, the live values they add themselves under Live values, and the rest the Scania team adds.
    /// </summary>
    public static string WhyNoNewVersion(IReadOnlyList<string>? blocking, bool forTeam)
    {
        var lines = blocking ?? [];
        if (lines.Count == 0)
        {
            return "the passport can't get a new version now; the team sees why on its battery page";
        }

        if (forTeam)
        {
            return $"the passport can't get a new version until this is filled in: {string.Join("; ", lines.Select(Item))}";
        }

        var theirs = lines.Where(line => ClusterAdminAdds(Attribute(line))).Select(Item).ToArray();
        var team = lines.Where(line => !ClusterAdminAdds(Attribute(line))).Select(Item).ToArray();
        return (theirs.Length, team.Length) switch
        {
            (> 0, 0) => $"the passport can't get a new version until you add {string.Join("; ", theirs)} under Live values",
            (0, _) => $"the passport can't get a new version until the Scania team adds {string.Join("; ", team)}. Ask the Scania team",
            _ => $"the passport can't get a new version until you add {string.Join("; ", theirs)} under Live values, and the Scania team adds {string.Join("; ", team)}. Ask the Scania team for theirs",
        };
    }

    /// <summary>One passport's outcome as a sentence, e.g. after a battery's own change.</summary>
    public static string For(PassportOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return outcome.Outcome switch
        {
            PublishOutcome.NewVersion => $"Version {outcome.Version} of the passport is published.",
            PublishOutcome.NothingChanged => $"The passport's published values didn't change, so it keeps version {outcome.Version}.",
            _ => $"The passport keeps version {outcome.Version} until this is filled in: {string.Join("; ", outcome.Blocking.Select(Item))}.",
        };
    }

    /// <summary>
    /// After a battery's live values are saved (round two, A3): they change the passport in place, unless a change that
    /// was waiting could now follow them, which publishes a new version.
    /// </summary>
    public static string AfterLiveValues(IReadOnlyList<(PassportOutcome Outcome, string SerialNumber)> outcomes) =>
        outcomes is [{ Outcome: { Outcome: PublishOutcome.NewVersion } published }]
            ? $"Version {published.Version} of the passport is published, with the saved changes that were waiting."
            : "Live values change the passport in place, without a new version.";

    /// <summary>
    /// The outcomes of a change that reached several published passports, named by serial number: how many got a new
    /// version, for which nothing changed, and which keep their version until what is listed is filled in.
    /// </summary>
    public static string ForMany(IReadOnlyList<(PassportOutcome Outcome, string SerialNumber)> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        var parts = new List<string>();
        var published = outcomes.Count(item => item.Outcome.Outcome == PublishOutcome.NewVersion);
        if (published > 0)
        {
            parts.Add($"{published} {(published == 1 ? "passport" : "passports")} got a new version.");
        }

        var unchanged = outcomes.Where(item => item.Outcome.Outcome == PublishOutcome.NothingChanged).Select(item => item.SerialNumber).ToArray();
        if (unchanged.Length > 0)
        {
            parts.Add($"Nothing changed for {string.Join(", ", unchanged)}: {(unchanged.Length == 1 ? "it keeps its" : "they keep their")} version.");
        }

        foreach (var (outcome, serial) in outcomes.Where(item => item.Outcome.Outcome == PublishOutcome.HeldBack))
        {
            parts.Add($"{serial} keeps version {outcome.Version} until this is filled in: {string.Join("; ", outcome.Blocking.Select(Item))}.");
        }

        return string.Join(" ", parts);
    }

    [GeneratedRegex(@"^(.+) is missing( \((.+)\))?$")]
    private static partial Regex Missing();

    [GeneratedRegex(@"^(#\d+|E\d+) ")]
    private static partial Regex Label();
}
