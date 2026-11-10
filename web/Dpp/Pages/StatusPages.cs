using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>
/// "Who places it on the market again?" (mock-up S4, step 4c-1): the battery waits for its new passport, or has it.
/// </summary>
/// <param name="Action">Where the card's forms post: the battery page plus "/successor".</param>
/// <param name="SuccessorHref">The new pack's page for this reader (the team's battery page, or the detail page); null until made.</param>
/// <param name="Operators">The economic operators the team picks from for Scania's new passport; empty for a cluster admin.</param>
public sealed record SuccessionView(
    string Status,
    string Action,
    bool Linked,
    string? SuccessorHref,
    bool SuccessorPublished,
    string? OtherOperatorId,
    string? OtherOperatorInfo,
    string? OtherPassport,
    IReadOnlyList<(string Id, string Name)> Operators,
    string DefaultOperatorId)
{
    /// <summary>Scania's new pack has been made (a draft until the team publishes it).</summary>
    public bool SuccessorMade => SuccessorHref is not null;

    /// <summary>
    /// Where "New passport" leads: Scania's new battery page, or the other operator's address when it is an http or https
    /// address (S01). Null for anything else, even a value stored before the check existed.
    /// </summary>
    public string? NewPassportLink => SuccessorHref ?? WebAddress.Safe(OtherPassport);

    /// <summary>The other operator's new passport as plain text when it can't be a link (an ID, or a value that isn't a web address); else null.</summary>
    public string? NewPassportText => SuccessorHref is null && OtherPassport is { } other && WebAddress.Safe(other) is null ? other : null;
}

/// <summary>The S5 line on a new passport's battery pages: the battery it replaces, and whether that one is inactive yet.</summary>
public sealed record PredecessorView(string SerialNumber, string Href, bool Inactive);

/// <summary>
/// The battery status card both battery pages show (mock-up S2 and S3, step 4b): the status and since when, the repair
/// history newest first, and the "Change status" window with the re-stated values filled in.
/// </summary>
/// <param name="CanChange">False for a draft or a waste battery; the window isn't offered then.</param>
/// <param name="RatedCapacityAh">The model's rated capacity (#57), for the fade suggestion; null when it has none.</param>
/// <param name="Remaining">The latest remaining capacity the battery system exported, which the suggestion starts with (step 5b).</param>
public sealed record StatusCardView(
    string Status,
    DateTime? Since,
    bool AfterRepair,
    IReadOnlyList<RepairEntry> Repairs,
    RecordEditorView Restated,
    bool CanChange,
    string? WhyNot,
    decimal? RatedCapacityAh,
    string Today,
    bool ForTeam,
    bool MayAddNote = false,
    SuccessionView? Succession = null,
    PredecessorView? Predecessor = null,
    RemainingCapacityReading? Remaining = null)
{
    /// <summary>Where "Add a note to the history" posts (decision 34).</summary>
    public string NoteAction => $"{Restated.SaveAction}/note";

    /// <summary>The status in words, capitalised: "Re-used".</summary>
    public string StatusWords => Status.Length == 0 ? Status : char.ToUpperInvariant(Status[0]) + Status[1..];

    /// <summary>The choices of the window's first step, in order, with what each does (decision 34).</summary>
    public IReadOnlyList<(StatusEvent What, string Label, string Help)> Choices
    {
        get
        {
            var choices = new List<(StatusEvent, string, string)>();
            if (Status != "waste" && Succession is null)
            {
                choices.Add((StatusEvent.Repaired, "Repaired", Status == "original"
                    ? "Status becomes re-used; the repair joins its history. Same passport."
                    : $"Status stays {Status}; the repair joins its history. Same passport."));
                choices.Add((StatusEvent.PlacedOnMarketAgain, "Prepared for re-use and placed on the market again", "Status re-used. A new, linked passport follows."));
                choices.Add((StatusEvent.Remanufactured, "Remanufactured", "A new, linked passport follows."));
                choices.Add((StatusEvent.Repurposed, "Repurposed", "For a new use. A new, linked passport follows."));
                choices.Add((StatusEvent.Waste, "Waste", "Final. The passport stays active until the team marks it for deletion after recycling."));
            }

            if (ForTeam)
            {
                choices.Add((StatusEvent.Corrected, "Correct the values", "For a mistyped value: the status stays, nothing joins the history, and a new version is published."));
            }

            return choices;
        }
    }
}

/// <summary>
/// Builds the status card and reads the status window for both battery pages (step 4b). The change itself is
/// <see cref="StatusChangeService"/>'s; the window's fields are the editors' own, so values are read and checked alike.
/// </summary>
public sealed class StatusPages(DppStore store, RecordEditor editor, StatusChangeService changes, SuccessionService succession, TimeProvider clock)
{
    /// <summary>The card of a battery; <paramref name="typed"/> is the window as typed after a refused change.</summary>
    /// <param name="href">The battery page's address; the window posts to it plus "/status".</param>
    public async Task<StatusCardView> CardAsync(PassportSources sources, bool active, string href, bool forTeam, EditorState? typed = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var pack = sources.Pack;
        var attributes = StatusChangeService.RestatedAttributes(sources.Model);
        var current = CurrentValues(sources, attributes);
        var view = new RecordEditorView(
            TargetKind.Pack,
            pack.Id,
            string.Empty,
            $"{href}/status",
            $"{href}#status",
            await editor.SectionsAsync(TargetKind.Pack, pack.Id, attributes, current, typed?.Post, typed?.Save.Refusals(editor.Forms), cancellationToken),
            [],
            0,
            editor.Forms.Languages,
            string.Empty,
            new RemovalView(string.Empty, null))
        {
            ForTeam = forTeam,
        };
        var status = StatusChangeService.CurrentStatus(pack) ?? "original";
        var passportStatus = await store.PassportStatusAsync(pack.Id, cancellationToken);
        var successionView = await SuccessionAsync(pack, href, forTeam, cancellationToken);
        var whyNot = DppStatuses.IsNoLongerActive(passportStatus) ? $"The passport is {DppStatuses.Words(passportStatus!).ToLowerInvariant()}: nothing on it changes any more."
            : !active ? "The status changes once the passport is published."
            : status == "waste" && !forTeam ? "The battery is waste, which is final."
            : successionView is not null && !forTeam ? "The battery is waiting for its new passport."
            : null;
        return new StatusCardView(
            status,
            pack.Values.GetValueOrDefault("batteryStatus")?.GeneratedAtUtc,
            pack.Repairs.Count > 0 && status != "waste" && pack.Succession is null,
            pack.Repairs.AsEnumerable().Reverse().ToArray(),
            view,
            whyNot is null,
            whyNot,
            ValueWords.Decimal(sources.Model.Values.GetValueOrDefault("ratedCapacity")?.Value?["amperehourMiliamperehourValue"]),
            DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            forTeam,
            MayAddNote: active,
            Succession: successionView,
            Predecessor: await PredecessorAsync(pack, forTeam, cancellationToken),
            Remaining: pack.RemainingCapacity);
    }

    private async Task<SuccessionView?> SuccessionAsync(BatteryPack pack, string href, bool forTeam, CancellationToken cancellationToken)
    {
        if (pack.Succession is not { } waiting)
        {
            return null;
        }

        string? successorHref = null;
        var published = false;
        if (waiting.SuccessorPackId is { } successorId && await store.GetAsync<BatteryPack>(successorId, cancellationToken) is { } successor)
        {
            published = await store.GetLatestVersionNumberAsync(successorId, cancellationToken) > 0;
            successorHref = forTeam
                ? BatteryPages.BatteryHref(successorId)
                : published ? PassportPages.DetailHref(BatteryPages.Text(successor, "uniqueProductIdentifier") ?? successorId) : null;
        }

        var operators = forTeam
            ? (await store.ListAsync<EconomicOperator>(cancellationToken)).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).Select(item => (item.Id, item.Name)).ToArray()
            : [];
        return new SuccessionView(
            waiting.Status,
            $"{href}/successor",
            waiting.LinkedAtUtc is not null,
            successorHref,
            published,
            waiting.OtherOperatorId,
            waiting.OtherOperatorInfo,
            waiting.OtherPassport,
            operators,
            pack.OperatorId);
    }

    private async Task<PredecessorView?> PredecessorAsync(BatteryPack pack, bool forTeam, CancellationToken cancellationToken)
    {
        if (pack.SuccessorOf is not { } predecessorId || await store.GetAsync<BatteryPack>(predecessorId, cancellationToken) is not { } predecessor)
        {
            return null;
        }

        return new PredecessorView(
            BatteryPages.Text(predecessor, "batterySerialNumber") ?? predecessorId,
            forTeam ? BatteryPages.BatteryHref(predecessorId) : PassportPages.DetailHref(BatteryPages.Text(predecessor, "uniqueProductIdentifier") ?? predecessorId),
            await store.PassportStatusAsync(predecessorId, cancellationToken) == DppStatuses.Inactive);
    }

    /// <summary>The team makes Scania's new passport; the message, and the new battery's page when it was made.</summary>
    public async Task<(string? Done, string? Problem, string? SuccessorId)> MakeSuccessorAsync(ChangeSender sender, string packId, string? operatorId, CancellationToken cancellationToken = default)
    {
        var (successorId, problem) = await succession.CreateSuccessorAsync(sender, packId, operatorId, cancellationToken);
        return problem is null
            ? ("The new passport is made as a draft. Type its warranty, check it and publish it; the old passport then becomes inactive.", null, successorId)
            : (null, $"The new passport wasn't made: {problem}.", null);
    }

    /// <summary>Another operator's details, saved or linked; the card's message.</summary>
    public async Task<(string? Done, string? Problem)> OtherOperatorAsync(ChangeSender sender, string packId, OtherOperator details, bool link, CancellationToken cancellationToken = default) =>
        await succession.SaveOtherOperatorAsync(sender, packId, details, link, cancellationToken) is { } problem
            ? (null, problem == SuccessionService.LinkNeedsPassport ? $"The details are saved, but not linked: {problem}." : $"Nothing was saved: {problem}.")
            : (link ? "Linked: the new passport is named, and this passport is inactive." : "The operator's details are saved.", null);

    /// <summary>The team cancels the new passport; the card's message.</summary>
    public async Task<(string? Done, string? Problem)> CancelSuccessorAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default) =>
        await succession.CancelAsync(sender, packId, cancellationToken) is { } problem
            ? (null, $"Nothing was cancelled: {problem}.")
            : ("The new passport is cancelled, and the old status is back.", null);

    /// <summary>Reads the window and makes the change; null when the battery doesn't exist.</summary>
    public async Task<StatusChangeResult?> ChangeAsync(
        ChangeSender sender,
        string packId,
        string? what,
        Func<string, string?> field,
        string? repairDate,
        string? repairDescription,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        var sources = await store.LoadSourcesAsync(packId, cancellationToken);
        if (sources is null)
        {
            return null;
        }

        var restated = StatusChangeService.RestatedAttributes(sources.Model).ToDictionary(
            attribute => attribute.Property,
            attribute => FormValues.Read(editor.Forms.For(attribute), field),
            StringComparer.Ordinal);
        return Enum.TryParse<StatusEvent>(what, ignoreCase: false, out var happened) && Enum.IsDefined(happened)
            ? await changes.ChangeAsync(sender, packId, new StatusChange(happened, restated, repairDate, repairDescription), cancellationToken)
            : new StatusChangeResult(true, null, [], "choose what happened");
    }

    /// <summary>Adds a note to the history; the card's message, done or why not.</summary>
    public async Task<(string? Done, string? Problem)> AddNoteAsync(ChangeSender sender, string packId, string? text, CancellationToken cancellationToken = default) =>
        await changes.AddNoteAsync(sender, packId, text, cancellationToken) is { } problem
            ? (null, $"The note wasn't added: {problem}.")
            : ("Note added to the history.", null);

    /// <summary>What the card says after the window was sent: done (with the new version) or why nothing changed, in plain words.</summary>
    public (string? Done, string? Problem) Message(StatusChangeResult result, int versionBefore)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Problem is not null)
        {
            var words = new EditorSave(EditorOutcome.Refused, result.Keys, []).Refusals(editor.Forms);
            var refused = result.Keys
                .Where(key => key.Outcome is KeyOutcome.Rejected or KeyOutcome.KeptEmpty && AttributeCatalog.FindByProperty(key.Key) is not null)
                .Select(key => $"{AttributeCatalog.FindByProperty(key.Key)!.Name}: {(words.TryGetValue(key.Key, out var refusal) ? refusal.Text : "it needs a value")}")
                .ToArray();
            return (null, $"Nothing was changed: {result.Problem}.{(refused.Length == 0 ? string.Empty : " " + string.Join(". ", refused.Select(text => text.TrimEnd('.'))) + ".")}");
        }

        return result.Version is { } version && version.Number > versionBefore
            ? ($"Saved: the battery is {result.NewStatus}. Version {version.Number} of the passport is published.", null)
            : ($"Saved: the battery is {result.NewStatus}. The passport's values didn't change, so it keeps version {versionBefore}.", null);
    }

    /// <summary>The re-stated values as the battery shows them now: its own once re-stated, else its model's (design 4.1).</summary>
    private static Dictionary<string, AttributeValue> CurrentValues(PassportSources sources, IReadOnlyList<AttributeDefinition> attributes)
    {
        var values = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        foreach (var attribute in attributes)
        {
            if ((sources.Pack.Values.GetValueOrDefault(attribute.Property) ?? sources.Model.Values.GetValueOrDefault(attribute.Property)) is { } value)
            {
                values[attribute.Property] = value;
            }
        }

        return values;
    }
}
