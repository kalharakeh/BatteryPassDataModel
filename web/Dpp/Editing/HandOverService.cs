using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>What asking for a hand-over did: the receiving cluster's name to confirm, the waiting hand-over, or why not.</summary>
public sealed record HandOverAsk(string? Problem, string? ReceiverName, HandOver? HandOver);

/// <summary>A hand-over waiting for a cluster admin's answer, with what the Registry card shows about it.</summary>
public sealed record WaitingHandOver(HandOver HandOver, string SerialNumber, string ModelName, string FromName);

/// <summary>
/// Hand-overs between clusters (design 7.8, decisions 9, 14 and 34; step 4d). A battery's cluster admin names the receiving
/// cluster by its ID, sees its name, and confirms; the battery stays with them until the receiving cluster's admins
/// accept, and the receiver sees nothing until then. Either side can end it: the sender (or the team) cancels, the
/// receiver declines. On acceptance the battery moves; the cluster isn't passport data, so no version is made.
/// </summary>
public sealed class HandOverService(DppStore store, ClusterRepository clusters, ApplicationAuditService audit, TimeProvider clock)
{
    private readonly DemoBoundary demo = new(store, clusters);

    public const string AskedEvent = "dpp.handover-asked";
    public const string AnsweredEvent = "dpp.handover-answered";
    public const string CancelledEvent = "dpp.handover-cancelled";

    public const string NoBattery = "the battery was not found";
    public const string NotYours = "only the battery's cluster admins hand it over";
    public const string TeamMovesDirectly = "the team moves a battery directly on its battery page";
    public const string OnlyActive = "only a battery with an active passport is handed over";
    public const string NoSuchCluster = "no cluster has this ID; ask the receiving customer for theirs";
    public const string SameCluster = "the battery is already in this cluster";
    public const string AlreadyWaiting = "a hand-over of this battery is already waiting";
    public const string NothingWaiting = "no hand-over of this battery is waiting";
    public const string NotTheReceiver = "only the receiving cluster's admins answer a hand-over";
    public const string NotWaiting = "this hand-over isn't waiting any more";

    /// <summary>Why an accept is refused once the passport stopped being active while it waited, for the receiver.</summary>
    public const string NoLongerActive = "the battery's passport is no longer active (it was replaced by a new passport or marked invalid), so it can't move to your cluster. Decline it, or ask the sending cluster to cancel";

    /// <summary>Names (and with <paramref name="confirm"/>, asks for) the hand-over of a battery to the cluster with this ID.</summary>
    public async Task<HandOverAsk> AskAsync(ChangeSender sender, string packId, string? toClusterId, bool confirm, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (sender.Kind == SenderKind.Team)
        {
            return Refused(TeamMovesDirectly);
        }

        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } pack)
        {
            return Refused(NoBattery);
        }

        // Cluster IDs are compared normalised, as ClusterBatteryPages does (the step 4 wrap-up's follow-up).
        if (sender.Kind != SenderKind.ClusterAdmin || pack.ClusterId is not { } stored || ClusterRepository.NormalizeClusterId(stored) is not { } from
            || !sender.ClusterIds.Contains(from)
            || !await demo.ReachesAsync(sender, pack, cancellationToken)) // finding R03
        {
            return Refused(NotYours);
        }

        if (await store.GetActiveVersionAsync(packId, cancellationToken) is null)
        {
            return Refused(OnlyActive);
        }

        var to = string.IsNullOrWhiteSpace(toClusterId) ? string.Empty : ClusterRepository.NormalizeClusterId(toClusterId);
        if (to == from)
        {
            return Refused(SameCluster);
        }

        if (to.Length == 0 || await clusters.GetClusterByIdAsync(to, cancellationToken) is not { } cluster)
        {
            return Refused(NoSuchCluster);
        }

        if (!DemoScope.MayMove(sender, ClusterRepository.IsDemo(cluster))) // a demo admin hands over only into a demo cluster (finding R03)
        {
            return Refused(NoSuchCluster);
        }

        if (await store.FindWaitingHandOverAsync(packId, cancellationToken) is not null)
        {
            return Refused(AlreadyWaiting);
        }

        var name = BsonHelpers.GetString(cluster, "name") is { Length: > 0 } named ? named : to;
        if (!confirm)
        {
            return new HandOverAsk(null, name, null);
        }

        var handOver = new HandOver
        {
            Id = $"handover-{Guid.NewGuid():N}",
            PackId = packId,
            FromClusterId = from,
            ToClusterId = to,
            State = HandOverStates.Waiting,
            AskedBy = sender.Who,
            AskedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        await store.InsertAsync(handOver, cancellationToken);
        await LogAsync(AskedEvent, sender, handOver, $"Battery {Serial(pack)}: hand-over to {name} asked", cancellationToken);
        return new HandOverAsk(null, name, handOver);
    }

    /// <summary>The receiving cluster's admin accepts (the battery moves) or declines. Null when done.</summary>
    public async Task<string?> AnswerAsync(ChangeSender sender, string handOverId, bool accept, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (await store.GetAsync<HandOver>(handOverId, cancellationToken) is not { } handOver)
        {
            return NotWaiting;
        }

        if (sender.Kind != SenderKind.ClusterAdmin || !sender.ClusterIds.Contains(ClusterRepository.NormalizeClusterId(handOver.ToClusterId)))
        {
            return NotTheReceiver;
        }

        var pack = await store.GetAsync<BatteryPack>(handOver.PackId, cancellationToken);
        if (pack is not null && !await demo.ReachesAsync(sender, pack, cancellationToken)) // a demo admin accepts only batteries inside the demo (finding R03)
        {
            return NotTheReceiver;
        }

        if (handOver.State != HandOverStates.Waiting || pack?.ClusterId is not { } now
            || ClusterRepository.NormalizeClusterId(now) != ClusterRepository.NormalizeClusterId(handOver.FromClusterId))
        {
            return NotWaiting;
        }

        // The passport may have stopped being active while it waited (a successor, a mark): it can't be managed then.
        if (accept && await store.GetActiveVersionAsync(handOver.PackId, cancellationToken) is null)
        {
            return NoLongerActive;
        }

        if (!await store.SetHandOverStateAsync(handOver.Id, accept ? HandOverStates.Accepted : HandOverStates.Declined, sender.Who, clock.GetUtcNow().UtcDateTime, cancellationToken))
        {
            return NotWaiting; // answered or cancelled meanwhile
        }

        if (accept)
        {
            await store.SetPackClusterAsync(handOver.PackId, handOver.ToClusterId, cancellationToken);
        }

        await LogAsync(AnsweredEvent, sender, handOver, $"Battery {Serial(pack)}: hand-over {(accept ? "accepted; it moved" : "declined; it stays")}", cancellationToken, accept);
        return null;
    }

    /// <summary>The sender's cluster admin, or the team, cancels the battery's waiting hand-over. Null when done.</summary>
    public async Task<string?> CancelAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (await store.FindWaitingHandOverAsync(packId, cancellationToken) is not { } handOver)
        {
            return NothingWaiting;
        }

        var mayCancel = await demo.ReachesAsync(sender, packId, cancellationToken) && (sender.Kind == SenderKind.Team // finding R03
            || (sender.Kind == SenderKind.ClusterAdmin && sender.ClusterIds.Contains(ClusterRepository.NormalizeClusterId(handOver.FromClusterId))));
        if (!mayCancel)
        {
            return NotYours;
        }

        if (!await store.SetHandOverStateAsync(handOver.Id, HandOverStates.Cancelled, sender.Who, clock.GetUtcNow().UtcDateTime, cancellationToken))
        {
            return NothingWaiting;
        }

        var pack = await store.GetAsync<BatteryPack>(packId, cancellationToken);
        await LogAsync(CancelledEvent, sender, handOver, $"Battery {Serial(pack)}: hand-over cancelled", cancellationToken);
        return null;
    }

    /// <summary>How many hand-overs wait for the sender's answer, for the Registry's entry in the top bar (redesign R3).</summary>
    public async Task<int> WaitingCountAsync(ChangeSender sender, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return sender.Kind != SenderKind.ClusterAdmin || sender.ClusterIds.Count == 0
            ? 0
            : (await WaitingHandOversAsync(sender, cancellationToken)).Count;
    }

    /// <summary>The hand-overs waiting for the sender; for a demo admin only those of batteries inside the demo (finding R03).</summary>
    private async Task<IReadOnlyList<HandOver>> WaitingHandOversAsync(ChangeSender sender, CancellationToken cancellationToken)
    {
        var waiting = await store.ListWaitingHandOversToAsync(sender.ClusterIds, cancellationToken);
        if (!sender.DemoOnly)
        {
            return waiting;
        }

        var inside = new List<HandOver>();
        foreach (var handOver in waiting)
        {
            if (await demo.ReachesAsync(sender, handOver.PackId, cancellationToken))
            {
                inside.Add(handOver);
            }
        }

        return inside;
    }

    /// <summary>The hand-overs waiting for the sender's answer: to a cluster they are admin of.</summary>
    public async Task<IReadOnlyList<WaitingHandOver>> WaitingForAsync(ChangeSender sender, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (sender.Kind != SenderKind.ClusterAdmin || sender.ClusterIds.Count == 0)
        {
            return [];
        }

        var waiting = new List<WaitingHandOver>();
        foreach (var handOver in await WaitingHandOversAsync(sender, cancellationToken))
        {
            var sources = await store.LoadSourcesAsync(handOver.PackId, cancellationToken);
            var from = await clusters.GetClusterByIdAsync(handOver.FromClusterId, cancellationToken);
            waiting.Add(new WaitingHandOver(
                handOver,
                sources is null ? handOver.PackId : Serial(sources.Pack),
                sources?.Model.Name ?? string.Empty,
                from is null ? handOver.FromClusterId : BsonHelpers.GetString(from, "name")));
        }

        return waiting;
    }

    private Task LogAsync(string eventType, ChangeSender sender, HandOver handOver, string message, CancellationToken cancellationToken, bool? accepted = null)
    {
        var metadata = new BsonDocument { ["handOver"] = handOver.Id, ["from"] = handOver.FromClusterId, ["to"] = handOver.ToClusterId };
        if (accepted is { } answer)
        {
            metadata["accepted"] = answer;
        }

        return audit.AppendApplicationAuditEventAsync(eventType, sender.Who, sender.Kind.ToString(), "form", message, metadata, entityType: "dpp-pack", entityId: handOver.PackId, cancellationToken: cancellationToken);
    }

    private static HandOverAsk Refused(string problem) => new(problem, null, null);

    private static string Serial(BatteryPack? pack) => pack is null ? "?" : BatteryPassWeb.Dpp.Pages.BatteryPages.Text(pack, "batterySerialNumber") ?? pack.Id;
}
