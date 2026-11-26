using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>One entry of a battery page's side menu (redesign R4, option B): one card of the page, shown alone.</summary>
/// <param name="Id">The card's id, also its anchor and the "section" query value: "live", "status", …</param>
/// <param name="Icon">The icon's id in Views/Shared/_Icons.cshtml, without "i-".</param>
/// <param name="Hint">One line under the title, e.g. "Nothing needs you"; null when a count says it.</param>
/// <param name="Count">A number beside the title, e.g. how many accident reports; null for none.</param>
public sealed record BatterySection(string Id, string Title, string Icon, string? Hint = null, int? Count = null);

/// <summary>The side menu as a page draws it: its sections, the open one, and the page they belong to.</summary>
public sealed record BatteryNav(IReadOnlyList<BatterySection> Sections, string Chosen, string Href);

/// <summary>
/// Which section of a battery page is open (redesign R4). One at a time, like the old app's side menu. The server
/// chooses, so a save comes back to its own section with its message, with or without JavaScript.
/// </summary>
public static class BatterySections
{
    public const string Overview = "overview";

    /// <summary>
    /// The open section: the one asked for (?section=), else the one whose card a form just sent (the controllers'
    /// card key), else the passport values after a values save, else the first (the overview).
    /// </summary>
    public static string Chosen(IReadOnlyList<BatterySection> sections, string? asked, string? card, bool valuesMessage)
    {
        ArgumentNullException.ThrowIfNull(sections);
        bool Has(string? id) => id is not null && sections.Any(section => section.Id == id);
        if (Has(asked))
        {
            return asked!;
        }

        if (card is not null && Has(Of(card)))
        {
            return Of(card);
        }

        return valuesMessage && Has("values") ? "values" : sections[0].Id;
    }

    /// <summary>The section a card's messages belong to: the publication, cluster and removal cards live in the overview.</summary>
    public static string Of(string card) => card switch
    {
        "publication" or "remove" or "cluster" or "mark" => Overview,
        "module-list" => "modules",
        _ => card,
    };

    /// <summary>The status section's hint: the status, and how many repairs.</summary>
    public static string StatusHint(StatusCardView status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var repairs = status.Repairs.Count(repair => !repair.IsCorrection);
        return repairs == 0 ? status.StatusWords : $"{status.StatusWords} · {Words.Count(repairs, "repair")}";
    }

    /// <summary>The live values section's hint: when the last one was measured, or that none was yet.</summary>
    public static string LiveHint(BatteryPack pack, RecordEditorView live)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(live);
        var last = live.Sections.SelectMany(section => section.Fields)
            .Select(field => pack.Values.GetValueOrDefault(field.Attribute.Property)?.GeneratedAtUtc)
            .Max();
        return last is { } at ? $"Last {ValueWords.Time(at)}" : "Nothing measured yet";
    }
}
