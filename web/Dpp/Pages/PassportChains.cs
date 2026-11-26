using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>
/// One passport of a battery placed on the market again (design 7.5): its place in the chain, its battery ID (#6), its
/// passport status (#2), when it was published and when a newer one replaced it.
/// </summary>
public sealed record ChainLink(int Number, string PackId, string Identifier, string Status, DateTime? FromUtc, DateTime? ToUtc, bool IsThis, bool IsNewest)
{
    /// <summary>Whether it was ever published; a draft has no public facts.</summary>
    public bool Published => FromUtc is not null;
}

/// <summary>Who reads the chain, which sets what each line holds and where it leads (redesign R6, the owner's item 12).</summary>
public enum ChainReader
{
    /// <summary>The team's battery page: every passport, its battery page and its history.</summary>
    Team,

    /// <summary>The detail page, for the team and market surveillance: every published passport, its detail page.</summary>
    Detail,

    /// <summary>
    /// The current cluster admin: public facts only (battery ID, passport status, dates, the public page), because an
    /// earlier passport may have belonged to another customer's cluster.
    /// </summary>
    ClusterAdmin,
}

/// <summary>A battery's chain of passports, as one reader sees it (redesign R6, the owner's item 12).</summary>
public sealed record ChainView(IReadOnlyList<ChainLink> Links, ChainReader Reader)
{
    /// <summary>The lines this reader sees: a cluster admin and the detail page only published passports.</summary>
    public IReadOnlyList<ChainLink> Shown => Reader == ChainReader.Team ? Links : Links.Where(link => link.Published).ToArray();
}

/// <summary>
/// "Passports of this battery" (redesign R6, the owner's item 12, "Team + MSA full, admin limited"): a battery placed on
/// the market again has a pack per passport with the same serial number, each new pack naming the one it replaces
/// (<see cref="BatteryPack.SuccessorOf"/>). The chain runs from the first passport to the newest.
/// </summary>
public static class PassportChains
{
    /// <summary>The battery's passports in order, oldest first; empty when it has only this one.</summary>
    public static async Task<IReadOnlyList<ChainLink>> ForAsync(DppStore store, BatteryPack pack, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(pack);
        var serial = BatteryPages.Text(pack, "batterySerialNumber");
        var packs = serial is null ? [pack] : await store.FindPacksBySerialNumberAsync(serial, cancellationToken);
        var byId = packs.Append(pack).GroupBy(each => each.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var first = pack;
        var seen = new HashSet<string>(StringComparer.Ordinal) { pack.Id };
        while (first.SuccessorOf is { } earlier && byId.TryGetValue(earlier, out var previous) && seen.Add(previous.Id))
        {
            first = previous;
        }

        var chain = new List<BatteryPack> { first };
        while (byId.Values.FirstOrDefault(each => each.SuccessorOf == chain[^1].Id && !chain.Contains(each)) is { } next)
        {
            chain.Add(next);
        }

        if (chain.Count < 2)
        {
            return [];
        }

        var links = new List<ChainLink>();
        for (var index = 0; index < chain.Count; index++)
        {
            var each = chain[index];
            var versions = await store.ListVersionsAsync(each.Id, cancellationToken);
            var latest = versions.MaxBy(version => version.Number);
            var status = latest?.DppStatus ?? "draft";
            links.Add(new ChainLink(
                index + 1,
                each.Id,
                BatteryPages.Text(each, "uniqueProductIdentifier") ?? string.Empty,
                status,
                versions.Count == 0 ? null : versions.Min(version => version.PublishedAtUtc),
                status == DppStatuses.Inactive ? latest!.LastUpdatedUtc : null,
                each.Id == pack.Id,
                index == chain.Count - 1));
        }

        return links;
    }
}
