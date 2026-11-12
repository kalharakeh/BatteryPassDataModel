using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Audit;

/// <summary>
/// The cluster admin's read-only History of a battery (redesign R6, the owner's choice 9; the managing session's privacy
/// rules). It shows only what happened while the battery was in their cluster, and its moves in and out; never more than
/// their battery page shows them: no other cluster's name, no team member's name, no value they may not read, no mark
/// reason. The log doesn't store an actor's cluster, so the battery's cluster at each moment is worked out from its
/// cluster changes and accepted hand-overs, back from the cluster it is in now.
/// </summary>
public sealed class ClusterHistory(ApplicationAuditService audit, AuditLines lines)
{
    /// <summary>The most lines shown, newest first.</summary>
    public const int Shown = 50;

    /// <summary>Events whose own line says nothing about another cluster or the team's internals.</summary>
    private static readonly HashSet<string> Described =
    [
        SavePipeline.AuditEventType,
        StatusChangeService.ChangedEvent,
        StatusChangeService.NoteAddedEvent,
        DocumentService.UploadedEvent,
        PublicationService.PublishedEvent,
        PublicationService.InactiveEvent,
        BatteryPages.ModulesSetEvent,
        PackCreationService.AuditEventType,
    ];

    /// <summary>Who did it, as a cluster admin may read it: their own cluster's people by name, everyone else by role.</summary>
    public static string Who(string actor, string role) => role switch
    {
        nameof(SenderKind.ClusterAdmin) => actor,
        nameof(SenderKind.BatterySystemFeed) => "Your battery system",
        nameof(SenderKind.Team) => "The manufacturer's team",
        nameof(SenderKind.MarketSurveillance) => "Market surveillance",
        _ => "The app",
    };

    public async Task<IReadOnlyList<AuditLine>> ForAsync(BatteryPack pack, IReadOnlySet<string> clusterIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(clusterIds);
        var events = await audit.ListEntityTimelineAsync("dpp-pack", pack.Id, cancellationToken);
        var picked = Grouped(Pick(events, pack.ClusterId, clusterIds)).Take(Shown).ToArray();
        var described = new Queue<AuditLine>(await lines.DescribeAsync(picked.Where(item => item.Own is null).Select(item => item.Event).ToArray(), cancellationToken));
        return picked.Select(item => item.Own ?? Readings(AfterOnly(described.Dequeue()), item.Readings)).ToArray(); // the described lines come back in order
    }

    /// <summary>The newest lines shown open; the others fold away (redesign R6 review: no list over 20 rows).</summary>
    public const int ShownOpen = 10;

    /// <summary>
    /// A run of saves of live values only, by the same sender, as one line: the newest, with how many readings it stands
    /// for (redesign R6 review: 28 demo readings filled the History; the Live values tab has them all).
    /// </summary>
    public static IEnumerable<(BsonDocument Event, AuditLine? Own, int Readings)> Grouped(IEnumerable<(BsonDocument Event, AuditLine? Own)> newestFirst)
    {
        (BsonDocument Event, AuditLine? Own, int Readings)? run = null;
        foreach (var item in newestFirst)
        {
            if (run is { } open && open.Own is null && item.Own is null && LiveOnly(open.Event) && LiveOnly(item.Event)
                && BsonHelpers.GetString(open.Event, "actor") == BsonHelpers.GetString(item.Event, "actor"))
            {
                run = open with { Readings = open.Readings + 1 };
                continue;
            }

            if (run is { } done)
            {
                yield return done;
            }

            run = (item.Event, item.Own, 1);
        }

        if (run is { } last)
        {
            yield return last;
        }
    }

    private static bool LiveOnly(BsonDocument item) =>
        BsonHelpers.GetString(item, "eventType") == SavePipeline.AuditEventType
        && item.TryGetValue("metadata", out var metadata) && metadata.IsBsonDocument
        && metadata.AsBsonDocument.TryGetValue("keys", out var keys) && keys.IsBsonArray && keys.AsBsonArray.Count > 0
        && keys.AsBsonArray.OfType<BsonDocument>().All(key => AttributeCatalog.FindByProperty(BsonHelpers.GetString(key, "key"))?.Change == ChangeRule.Live);

    private static AuditLine Readings(AuditLine line, int readings) =>
        readings < 2 ? line : line with { Change = $"the latest of {readings} live readings" };

    /// <summary>
    /// A line with the new values only: a value before a change may date from before the battery was theirs, e.g. the
    /// first change after a hand-over in, so "before" is never shown.
    /// </summary>
    public static AuditLine AfterOnly(AuditLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var change = line.Change is { } words && words.LastIndexOf(" → ", StringComparison.Ordinal) is var arrow and >= 0 ? words[(arrow + 3)..] : line.Change;
        return line with { Change = change, Values = line.Values.Select(row => row with { Before = string.Empty }).ToArray() };
    }

    /// <summary>
    /// The events a cluster admin of <paramref name="mine"/> may see, newest first, each a cleaned copy (the team's audit
    /// log reads the same documents) or a line of its own for a move, a hand-over or a mark.
    /// </summary>
    public static IEnumerable<(BsonDocument Event, AuditLine? Own)> Pick(IReadOnlyList<BsonDocument> newestFirst, string? clusterNow, IReadOnlySet<string> mine)
    {
        ArgumentNullException.ThrowIfNull(newestFirst);
        ArgumentNullException.ThrowIfNull(mine);
        bool Mine(string? cluster) => cluster is not null && mine.Contains(cluster);
        var cluster = clusterNow; // the battery's cluster at the moment of the event being read, going back in time
        foreach (var original in newestFirst)
        {
            var type = BsonHelpers.GetString(original, "eventType");
            var metadata = original.TryGetValue("metadata", out var value) && value.IsBsonDocument ? value.AsBsonDocument : new BsonDocument();
            var from = metadata.TryGetValue("from", out var f) && f.IsString ? f.AsString : null;
            var to = metadata.TryGetValue("to", out var t) && t.IsString ? t.AsString : null;
            var role = BsonHelpers.GetString(original, "actorRole");
            var who = Who(BsonHelpers.GetString(original, "actor"), role);
            var accepted = metadata.TryGetValue("accepted", out var a) && a.IsBoolean && a.AsBoolean;

            // A hand-over is asked and cancelled by the sending cluster's admin and answered by the receiving one's: when
            // that side isn't theirs, the actor is "Another cluster", never its name or its people.
            string Side(bool theirSideIsMine) => role == nameof(SenderKind.ClusterAdmin) && !theirSideIsMine ? "Another cluster" : who;

            if (type == BatteryPages.ClusterChangedEvent || (type == HandOverService.AnsweredEvent && accepted))
            {
                if (Mine(from) || Mine(to))
                {
                    var words = type == BatteryPages.ClusterChangedEvent
                        ? Mine(to) ? " moved the battery to your cluster" : " moved the battery to another cluster"
                        : Mine(to) ? " accepted the battery's hand-over to your cluster" : " accepted the battery's hand-over: it moved to another cluster";
                    yield return (original, Line(original, type == BatteryPages.ClusterChangedEvent ? who : Side(Mine(to)), words));
                }

                cluster = from;
                continue;
            }

            if (type is HandOverService.AskedEvent or HandOverService.CancelledEvent or HandOverService.AnsweredEvent)
            {
                if (Mine(from) || Mine(to))
                {
                    var words = type switch
                    {
                        HandOverService.AskedEvent => Mine(from) ? " asked another cluster to accept the battery" : " asked your cluster to accept the battery",
                        HandOverService.CancelledEvent => " cancelled the battery's hand-over",
                        _ => " declined the battery's hand-over",
                    };
                    yield return (original, Line(original, Side(type == HandOverService.AnsweredEvent ? Mine(to) : Mine(from)), words));
                }

                continue;
            }

            if (!Mine(cluster))
            {
                continue;
            }

            if (type is PassportMarks.InvalidEvent or PassportMarks.DeletionEvent or PassportMarks.UndoneEvent)
            {
                // The fact and the day only: a mark's reason is the team's and market surveillance's (decision 34).
                var words = type switch
                {
                    PassportMarks.InvalidEvent => " marked the passport invalid",
                    PassportMarks.DeletionEvent => " marked the passport for deletion",
                    _ => " undid the passport's mark: it is active again",
                };
                yield return (original, Line(original, who, words));
                continue;
            }

            if (type == DocumentService.WithdrawnEvent)
            {
                // The fact, the file's name and the day: not the team member's name, and not the reason, which is the team's.
                yield return (original, Line(original, who, " withdrew an accident report") with { Change = metadata.TryGetValue("fileName", out var file) && file.IsString ? file.AsString : null });
                continue;
            }

            if (!Described.Contains(type))
            {
                continue; // e.g. a refused publish or upload: the team's own working
            }

            var copy = original.DeepClone().AsBsonDocument;
            copy["actor"] = who; // no "@": the line builder looks up no one's name
            copy["actorRole"] = string.Empty;
            if (type == SavePipeline.AuditEventType)
            {
                var keys = metadata.TryGetValue("keys", out var k) && k.IsBsonArray ? k.AsBsonArray : [];
                var readable = new BsonArray(keys.OfType<BsonDocument>().Where(key => Readable(BsonHelpers.GetString(key, "key"))));
                if (readable.Count == 0)
                {
                    continue; // only values they may not read changed
                }

                copy["metadata"]["keys"] = readable;
            }

            yield return (copy, null);
        }
    }

    /// <summary>A value a cluster admin may read: public, purchaser or shared data; not the authorities' (#7, #12, #13, #29).</summary>
    private static bool Readable(string property) =>
        AttributeCatalog.All.FirstOrDefault(attribute => attribute.Property == property) is { } attribute && attribute.Audience != Audience.Authorities;

    private static AuditLine Line(BsonDocument original, string who, string words)
    {
        var when = DateTimeOffset.TryParse(BsonHelpers.GetString(original, "createdAt"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : (DateTimeOffset?)null;
        return new AuditLine(BsonHelpers.GetString(original, "eventId") is { Length: > 0 } id ? id : Guid.NewGuid().ToString("N"), BsonHelpers.GetString(original, "eventType"), when, AuditIcon.Other, false,
            [new AuditWord(who, Strong: true), new AuditWord(words)], null, [], [], [], string.Empty);
    }
}
