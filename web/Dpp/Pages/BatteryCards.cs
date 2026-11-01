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
public sealed record BatteryCards(
    BatteryPack Pack,
    string Href,
    RecordEditorView Live,
    IReadOnlyList<DppDocument> AccidentReports,
    IReadOnlyList<VersionRow> Versions,
    IReadOnlyList<string> Languages,
    bool ForTeam,
    string? ModuleDescriptionHref = null,
    bool HasModuleDescription = false)
{
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
