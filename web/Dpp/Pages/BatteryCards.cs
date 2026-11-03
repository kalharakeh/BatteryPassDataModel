using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>
/// The cards the team's battery page and the cluster admin's share (step 4a): live values, modules, accident reports
/// and versions, drawn by one partial view each. Forms post to <see cref="Href"/> plus "/live" or "/accidents"; the
/// team's page also edits the modules.
/// </summary>
/// <param name="ForTeam">The team's page: it edits the module list and links the model's module description.</param>
/// <param name="ModuleDescriptionHref">Where the team types the model's module description; null on the cluster admin's page.</param>
/// <param name="FrozenAs">The DPP status (#2) of a passport that is no longer active (inactive, invalid or marked for
/// deletion); null while it is active or a draft.</param>
/// <param name="NewPassportHref">An inactive passport's new passport, where the battery's reports go now (redesign R5); null otherwise.</param>
public sealed record BatteryCards(
    BatteryPack Pack,
    string Href,
    RecordEditorView Live,
    IReadOnlyList<DppDocument> AccidentReports,
    IReadOnlyList<VersionRow> Versions,
    IReadOnlyList<string> Languages,
    bool ForTeam,
    string? ModuleDescriptionHref = null,
    bool HasModuleDescription = false,
    string? FrozenAs = null,
    string? NewPassportHref = null)
{
    /// <summary>Whether nothing on the passport changes any more (step 4c).</summary>
    public bool Frozen => FrozenAs is not null;

    /// <summary>Why nothing on the passport changes, in words that fit its status (redesign R1); null while it can change.</summary>
    public string? FrozenNote => FrozenAs switch
    {
        null => null,
        DppStatuses.Invalid => $"The passport is invalid: nothing on it changes until {Team} undoes it.",
        DppStatuses.MarkedForDeletion => $"The passport is marked for deletion: nothing on it changes until {Team} undoes it.",
        _ => "The passport is no longer active: nothing on it changes any more. The battery's data continue on its new passport.",
    };

    /// <summary>Who the team is, in the reader's words.</summary>
    public string Team => ForTeam ? "your team" : "the team";

    /// <summary>A file size in plain words.</summary>
    public static string Size(long bytes) => bytes < 1024
        ? $"{bytes} bytes"
        : bytes < 1048576
            ? $"{(bytes / 1024.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} KB"
            : $"{(bytes / 1048576.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} MB";

    /// <summary>Where a stored value came from, in plain words.</summary>
    public static string Source(string? source) => source switch
    {
        "form" => "typed on a battery page",
        "upload" => "from an upload",
        "feed" => "from the battery system",
        "system" => "set by the app",
        "seed" => "test data",
        _ => source ?? "not recorded",
    };
}
