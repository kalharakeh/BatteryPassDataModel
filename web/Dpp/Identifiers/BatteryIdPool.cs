using System.Globalization;
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Dpp.Identifiers;

/// <summary>One range of a release: "VPC-12221" to "VPC-12230", 10 IDs.</summary>
public sealed record IdRange(string? Start, string? End, string? Quantity)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Start) && string.IsNullOrWhiteSpace(End) && string.IsNullOrWhiteSpace(Quantity);
}

/// <summary>What the team asks to release: ranges, a pasted list, or both, under one reference.</summary>
public sealed record ReleaseRequest(string? Reference, string? ModelId, IReadOnlyList<IdRange> Ranges, string? List, bool IsDemo = false);

/// <summary>Why one serial number (or one range) can't be released.</summary>
public sealed record IdProblem(string What, string Reason);

/// <summary>A release's outcome: all of it, or nothing and every reason.</summary>
public sealed record ReleaseResult(int Released, string? ReleaseId, IReadOnlyList<IdProblem> Problems, string? Problem)
{
    public bool Done => Released > 0;
}

/// <summary>One release, as the list shows it.</summary>
public sealed record ReleaseSummary(string ReleaseId, string Reference, string? ModelId, string ReleasedBy, DateTime ReleasedAtUtc, IReadOnlyDictionary<string, int> Counts)
{
    public int Total => Counts.Values.Sum();

    public int Count(string status) => Counts.GetValueOrDefault(status);
}

/// <summary>Which released IDs the list shows.</summary>
public sealed record ReleasedIdFilter(string? Status = null, string? Reference = null, string? ModelId = null, string? Query = null);

/// <summary>
/// The battery IDs the team releases in advance (step 6d, the owner, 6 Nov 2026). A release is checked whole and is
/// all-or-nothing; each serial number gets its #6 at release, fixed for life. A battery is created by picking an available
/// one; a draft may swap it for another; a voided one is never reused.
/// </summary>
public sealed partial class BatteryIdPool(DppStore store, IIdentifierScheme identifiers, ApplicationAuditService audit, TimeProvider clock)
{
    public const string ReleasedEvent = "dpp.ids-released";
    public const string VoidedEvent = "dpp.ids-voided";
    public const string SwappedEvent = "dpp.id-swapped";

    /// <summary>The most IDs one release may hold.</summary>
    public const int MaxPerRelease = 5000;

    public const int MaxReference = 120;

    public const int MaxReason = 200;

    /// <summary>Why a release was refused when another took some of its serial numbers at the same moment.</summary>
    public const string RaceLost = "another release took some of these serial numbers at the same moment; nothing was released, try again";

    public async Task<ReleaseResult> ReleaseAsync(string who, ReleaseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reference = request.Reference?.Trim() ?? string.Empty;
        if (reference.Length == 0)
        {
            return Refused("give the release a reference, such as the purchase order");
        }

        if (reference.Length > MaxReference)
        {
            return Refused($"the reference is longer than {MaxReference} characters");
        }

        if (request.ModelId is { Length: > 0 } modelId && await store.GetAsync<BatteryModel>(modelId, cancellationToken) is null)
        {
            return Refused("that model doesn't exist");
        }

        var problems = new List<IdProblem>();
        var serials = new List<string>();
        foreach (var range in request.Ranges.Where(range => !range.IsEmpty))
        {
            serials.AddRange(Expand(range, problems));
        }

        serials.AddRange((request.List ?? string.Empty).Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));
        if (serials.Count == 0 && problems.Count == 0)
        {
            return Refused("give a range or at least one serial number");
        }

        if (serials.Count > MaxPerRelease)
        {
            return Refused($"a release holds at most {MaxPerRelease} IDs; this one has {serials.Count}");
        }

        // Each serial number on its own, then against each other, then against the pool and the batteries.
        var wanted = new List<(string Serial, string Key, string Identifier)>();
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var serial in serials)
        {
            var made = identifiers.ItemFor(serial, request.IsDemo);
            if (made.Identifier is not { } identifier)
            {
                problems.Add(new IdProblem(serial, made.Problem!));
                continue;
            }

            var key = IdentifierForms.Clean(serial);
            if (seen.TryGetValue(key, out var first))
            {
                problems.Add(new IdProblem(serial, first == serial ? "it is in this release twice" : $"it is in this release twice, also as {first}"));
                continue;
            }

            seen[key] = serial;
            wanted.Add((serial, key, identifier));
        }

        var keys = wanted.Select(item => item.Key).ToArray();
        var pooled = await store.ReleasedIds.Find(Builders<ReleasedBatteryId>.Filter.In(item => item.Id, keys)).ToListAsync(cancellationToken);
        foreach (var item in pooled)
        {
            problems.Add(new IdProblem(seen[item.Id], $"already released ({ReleasedIdStatuses.Words(item.Status)}, {item.Reference})"));
        }

        var released = pooled.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var batteries = await BatteriesByKeyAsync(cancellationToken);
        foreach (var (serial, key, _) in wanted)
        {
            if (!released.Contains(key) && batteries.TryGetValue(key, out var battery)) // one reason per serial number
            {
                problems.Add(new IdProblem(serial, $"battery {battery} already has this serial number or battery ID"));
            }
        }

        if (problems.Count > 0)
        {
            return new ReleaseResult(0, null, problems, $"{Words(problems.Count, "serial number or range", "serial numbers or ranges")} can't be released");
        }

        var releaseId = $"release-{Guid.NewGuid():N}";
        var now = clock.GetUtcNow().UtcDateTime;
        var records = wanted.Select(item => new ReleasedBatteryId
        {
            Id = item.Key,
            SerialNumber = item.Serial,
            Identifier = item.Identifier,
            ReleaseId = releaseId,
            Reference = reference,
            ModelId = string.IsNullOrEmpty(request.ModelId) ? null : request.ModelId,
            Status = ReleasedIdStatuses.Available,
            ReleasedBy = who,
            ReleasedAtUtc = now,
            IsDemo = request.IsDemo,
        }).ToList();
        if (!await store.InsertReleaseAsync(records, cancellationToken))
        {
            // Another release took one of these at the same moment (the _id is unique), so this one went in whole or not at all.
            return Refused(RaceLost);
        }

        await audit.AppendApplicationAuditEventAsync(
            ReleasedEvent,
            who,
            "Team",
            "battery-ids",
            $"Released {Words(records.Count, "battery ID", "battery IDs")} ({reference}): {Span(records)}",
            new BsonDocument { ["releaseId"] = releaseId, ["reference"] = reference, ["count"] = records.Count },
            entityType: "dpp-battery-ids",
            entityId: releaseId,
            cancellationToken: cancellationToken);
        return new ReleaseResult(records.Count, releaseId, [], null);
    }

    /// <summary>The releases, newest first, with how many of their IDs are in each status.</summary>
    public async Task<IReadOnlyList<ReleaseSummary>> ReleasesAsync(CancellationToken cancellationToken = default)
    {
        var all = await store.ReleasedIds.Find(FilterDefinition<ReleasedBatteryId>.Empty).ToListAsync(cancellationToken);
        return all.GroupBy(item => item.ReleaseId)
            .Select(group =>
            {
                var first = group.First();
                return new ReleaseSummary(group.Key, first.Reference, first.ModelId, first.ReleasedBy, first.ReleasedAtUtc,
                    group.GroupBy(item => item.Status).ToDictionary(status => status.Key, status => status.Count(), StringComparer.Ordinal));
            })
            .OrderByDescending(release => release.ReleasedAtUtc)
            .ToArray();
    }

    /// <summary>The released IDs the filter keeps, newest release first, then by serial number.</summary>
    public async Task<IReadOnlyList<ReleasedBatteryId>> ListAsync(ReleasedIdFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var where = Builders<ReleasedBatteryId>.Filter.Empty;
        if (!string.IsNullOrEmpty(filter.Status))
        {
            where &= Builders<ReleasedBatteryId>.Filter.Eq(item => item.Status, filter.Status);
        }

        if (!string.IsNullOrEmpty(filter.Reference))
        {
            where &= Builders<ReleasedBatteryId>.Filter.Eq(item => item.ReleaseId, filter.Reference);
        }

        if (!string.IsNullOrEmpty(filter.ModelId))
        {
            where &= filter.ModelId == "any"
                ? Builders<ReleasedBatteryId>.Filter.Eq(item => item.ModelId, null)
                : Builders<ReleasedBatteryId>.Filter.Eq(item => item.ModelId, filter.ModelId);
        }

        if (IdentifierForms.Clean(filter.Query) is { Length: > 0 } query)
        {
            where &= Builders<ReleasedBatteryId>.Filter.Regex(item => item.Id, new BsonRegularExpression(Regex.Escape(query), string.Empty));
        }

        return await store.ReleasedIds.Find(where).SortByDescending(item => item.ReleasedAtUtc).ThenBy(item => item.Id).ToListAsync(cancellationToken);
    }

    /// <summary>The IDs a battery of <paramref name="modelId"/> may take: its model's first, then those for any model.</summary>
    public async Task<IReadOnlyList<ReleasedBatteryId>> AvailableAsync(string? modelId = null, bool? demo = null, CancellationToken cancellationToken = default)
    {
        var where = Builders<ReleasedBatteryId>.Filter.Eq(item => item.Status, ReleasedIdStatuses.Available);
        if (demo is { } isDemo)
        {
            where &= Builders<ReleasedBatteryId>.Filter.Eq(item => item.IsDemo, isDemo);
        }

        var all = await store.ReleasedIds.Find(where).ToListAsync(cancellationToken);
        return all.Where(item => modelId is null || item.ModelId is null || item.ModelId == modelId)
            .OrderBy(item => item.IsDemo ? 1 : 0)
            .ThenBy(item => item.ModelId is null ? 1 : 0)
            .ThenBy(item => item.ReleasedAtUtc)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public Task<ReleasedBatteryId?> GetAsync(string serialNumber, CancellationToken cancellationToken = default) =>
        store.GetAsync<ReleasedBatteryId>(IdentifierForms.Clean(serialNumber), cancellationToken);

    /// <summary>A scanned or typed ID or serial number that is released but not published: its record, else null.</summary>
    public async Task<ReleasedBatteryId?> FindUnpublishedAsync(string text, CancellationToken cancellationToken = default)
    {
        var wanted = text?.Trim() ?? string.Empty;
        if (wanted.Length == 0)
        {
            return null;
        }

        var item = IdentifierForms.Short(wanted);
        var where = Builders<ReleasedBatteryId>.Filter.Eq(released => released.Id, IdentifierForms.Clean(wanted))
            | Builders<ReleasedBatteryId>.Filter.Eq(released => released.Identifier, wanted);
        if (IdentifierForms.IsItem(item))
        {
            where |= Builders<ReleasedBatteryId>.Filter.Regex(released => released.Identifier, new BsonRegularExpression($"/{IdentifierForms.DataIdentifier}/{Regex.Escape(item)}$", string.Empty));
        }

        var found = await store.ReleasedIds.Find(where).FirstOrDefaultAsync(cancellationToken);
        return found is { Status: not ReleasedIdStatuses.Published } ? found : null;
    }

    /// <summary>
    /// Gives an available ID to a draft of <paramref name="modelId"/>, in one step so that no one else can; null when it
    /// isn't available (taken or voided first) or is for another model.
    /// </summary>
    public async Task<ReleasedBatteryId?> PickAsync(string serialNumber, string packId, string? modelId, CancellationToken cancellationToken = default) =>
        await store.ReleasedIds.FindOneAndUpdateAsync(
            Builders<ReleasedBatteryId>.Filter.Eq(item => item.Id, IdentifierForms.Clean(serialNumber))
                & Builders<ReleasedBatteryId>.Filter.Eq(item => item.Status, ReleasedIdStatuses.Available)
                & (Builders<ReleasedBatteryId>.Filter.Eq(item => item.ModelId, null) | Builders<ReleasedBatteryId>.Filter.Eq(item => item.ModelId, modelId)),
            Builders<ReleasedBatteryId>.Update.Set(item => item.Status, ReleasedIdStatuses.Used).Set(item => item.PackId, packId),
            new FindOneAndUpdateOptions<ReleasedBatteryId> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

    /// <summary>The released ID a battery took, while it is a draft or published; null for a battery made otherwise.</summary>
    public Task<ReleasedBatteryId?> OfAsync(string packId, CancellationToken cancellationToken = default) =>
        store.ReleasedIds.Find(item => item.PackId == packId).FirstOrDefaultAsync(cancellationToken)!;

    /// <summary>
    /// A draft takes another available ID (the owner's "swap while draft"): it gets that serial number and battery ID, and
    /// its old one is available again. Null when done, else why not. After publication the ID is fixed.
    /// </summary>
    public async Task<string?> SwapAsync(string who, string packId, string? serialNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            return "choose the battery ID it takes instead";
        }

        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } pack)
        {
            return "the battery wasn't found";
        }

        if (await store.GetLatestVersionNumberAsync(packId, cancellationToken) > 0)
        {
            return "its passport is published, so its battery ID is fixed";
        }

        if (await OfAsync(packId, cancellationToken) is not { Status: ReleasedIdStatuses.Used } old)
        {
            return "its battery ID wasn't picked from the released IDs";
        }

        var model = (await store.GetAsync<ProductionBatch>(pack.BatchId, cancellationToken))?.ModelId;
        if (await PickAsync(serialNumber, packId, model, cancellationToken) is not { } picked)
        {
            return "that battery ID isn't available for this battery's model any more; pick another";
        }

        var now = clock.GetUtcNow().UtcDateTime;
        await store.SetValuesAsync<BatteryPack>(packId, new Dictionary<string, Values.AttributeValue>(StringComparer.Ordinal)
        {
            ["batterySerialNumber"] = new() { Value = System.Text.Json.Nodes.JsonValue.Create(picked.SerialNumber), GeneratedAtUtc = now, Source = "form", Who = who },
            ["uniqueProductIdentifier"] = new() { Value = System.Text.Json.Nodes.JsonValue.Create(picked.Identifier), GeneratedAtUtc = now, Source = "system", Who = who },
        }, cancellationToken);
        await store.ReleasedIds.UpdateOneAsync(
            item => item.Id == old.Id && item.PackId == packId,
            Builders<ReleasedBatteryId>.Update.Set(item => item.Status, ReleasedIdStatuses.Available).Set(item => item.PackId, null),
            cancellationToken: cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            SwappedEvent,
            who,
            "Team",
            "battery-ids",
            $"Draft battery {old.SerialNumber} now has battery ID {picked.SerialNumber}; {old.SerialNumber} is available again",
            new BsonDocument { ["from"] = old.Id, ["to"] = picked.Id },
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        return null;
    }

    /// <summary>A draft gives its ID back (swapped, or the draft removed): it is available again.</summary>
    public Task GiveBackAsync(string packId, CancellationToken cancellationToken = default) =>
        store.ReleasedIds.UpdateManyAsync(
            Builders<ReleasedBatteryId>.Filter.Eq(item => item.PackId, packId) & Builders<ReleasedBatteryId>.Filter.Eq(item => item.Status, ReleasedIdStatuses.Used),
            Builders<ReleasedBatteryId>.Update.Set(item => item.Status, ReleasedIdStatuses.Available).Set(item => item.PackId, null),
            cancellationToken: cancellationToken);

    /// <summary>The battery's passport is published: its ID is fixed for life.</summary>
    public Task MarkPublishedAsync(string packId, CancellationToken cancellationToken = default) =>
        store.ReleasedIds.UpdateManyAsync(
            Builders<ReleasedBatteryId>.Filter.Eq(item => item.PackId, packId) & Builders<ReleasedBatteryId>.Filter.Eq(item => item.Status, ReleasedIdStatuses.Used),
            Builders<ReleasedBatteryId>.Update.Set(item => item.Status, ReleasedIdStatuses.Published),
            cancellationToken: cancellationToken);

    /// <summary>Voids one available ID, for good; null when done, else why not.</summary>
    public async Task<string?> VoidAsync(string who, string serialNumber, string? reason, CancellationToken cancellationToken = default)
    {
        if (Reason(reason) is not { } why)
        {
            return "give a reason, such as a damaged label";
        }

        var item = await GetAsync(serialNumber, cancellationToken);
        if (item is null)
        {
            return "that ID isn't released";
        }

        if (item.Status != ReleasedIdStatuses.Available)
        {
            return $"only an available ID can be voided; this one is {ReleasedIdStatuses.Words(item.Status).ToLowerInvariant()}";
        }

        var voided = await VoidWhereAsync(who, why, Builders<ReleasedBatteryId>.Filter.Eq(released => released.Id, item.Id), cancellationToken);
        if (voided == 0)
        {
            return "it was just taken by a battery";
        }

        await LogVoidAsync(who, why, item.ReleaseId, $"Voided battery ID {item.SerialNumber} ({item.Reference}): {why}", cancellationToken);
        return null;
    }

    /// <summary>Voids every still available ID of one release; the count voided.</summary>
    public async Task<(long Voided, string? Problem)> VoidUnusedAsync(string who, string releaseId, string? reason, CancellationToken cancellationToken = default)
    {
        if (Reason(reason) is not { } why)
        {
            return (0, "give a reason, such as an order cancelled");
        }

        var first = await store.ReleasedIds.Find(item => item.ReleaseId == releaseId).FirstOrDefaultAsync(cancellationToken);
        if (first is null)
        {
            return (0, "that release doesn't exist");
        }

        var voided = await VoidWhereAsync(who, why, Builders<ReleasedBatteryId>.Filter.Eq(item => item.ReleaseId, releaseId), cancellationToken);
        if (voided > 0)
        {
            await LogVoidAsync(who, why, releaseId, $"Voided the {Words((int)voided, "unused battery ID", "unused battery IDs")} of release {first.Reference}: {why}", cancellationToken);
        }

        return (voided, voided == 0 ? "the release has no available IDs left" : null);
    }

    /// <summary>The list as an Excel file (text cells, the release time as a date): one row per ID, for label printers and mail merge.</summary>
    public static byte[] Export(IReadOnlyList<ReleasedBatteryId> items, IReadOnlyDictionary<string, string> modelNames) =>
        XlsxWriter.Write(
            "Battery IDs",
            ["Serial number", "Battery ID", "QR code address", "Model", "Reference", "Released by", "Released (UTC)", "Status"],
            items.Select(item => (IReadOnlyList<object?>)
            [
                item.SerialNumber,
                IdentifierForms.Display(item.Identifier),
                item.Identifier,
                item.ModelId is null ? "Any model" : modelNames.GetValueOrDefault(item.ModelId, item.ModelId),
                item.Reference,
                item.ReleasedBy,
                item.ReleasedAtUtc, // a real date cell
                ReleasedIdStatuses.Words(item.Status),
            ]).ToArray());

    /// <summary>A range's serial numbers, or its problem: same letters, same digit count, and the count matches.</summary>
    public static IReadOnlyList<string> Expand(IdRange range, ICollection<IdProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(range);
        ArgumentNullException.ThrowIfNull(problems);
        var start = range.Start?.Trim() ?? string.Empty;
        var end = range.End?.Trim() ?? string.Empty;
        var label = $"{(start.Length > 0 ? start : "?")} to {(end.Length > 0 ? end : "?")}";
        if (start.Length == 0 || end.Length == 0 || !int.TryParse(range.Quantity?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) || quantity < 1)
        {
            problems.Add(new IdProblem(label, "a range needs a start, an end and a quantity"));
            return [];
        }

        var first = Numbered().Match(start);
        var last = Numbered().Match(end);
        if (!first.Success || !last.Success)
        {
            problems.Add(new IdProblem(label, "a range's start and end end in a number"));
            return [];
        }

        var prefix = first.Groups["prefix"].Value;
        var width = first.Groups["number"].Value.Length;
        if (last.Groups["prefix"].Value != prefix || last.Groups["number"].Value.Length != width)
        {
            problems.Add(new IdProblem(label, "the start and end must have the same letters before the number, and as many digits"));
            return [];
        }

        if (!long.TryParse(first.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var from)
            || !long.TryParse(last.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var to))
        {
            problems.Add(new IdProblem(label, "the number is too long"));
            return [];
        }

        if (to < from)
        {
            problems.Add(new IdProblem(label, "the end comes before the start"));
            return [];
        }

        var count = to - from + 1;
        if (count != quantity)
        {
            problems.Add(new IdProblem(label, $"that is {count.ToString(CultureInfo.InvariantCulture)} IDs, not {quantity.ToString(CultureInfo.InvariantCulture)}"));
            return [];
        }

        if (count > MaxPerRelease)
        {
            problems.Add(new IdProblem(label, $"a release holds at most {MaxPerRelease} IDs"));
            return [];
        }

        return Enumerable.Range(0, (int)count).Select(step => prefix + (from + step).ToString(CultureInfo.InvariantCulture).PadLeft(width, '0')).ToArray();
    }

    private async Task<long> VoidWhereAsync(string who, string why, FilterDefinition<ReleasedBatteryId> which, CancellationToken cancellationToken)
    {
        var result = await store.ReleasedIds.UpdateManyAsync(
            which & Builders<ReleasedBatteryId>.Filter.Eq(item => item.Status, ReleasedIdStatuses.Available),
            Builders<ReleasedBatteryId>.Update
                .Set(item => item.Status, ReleasedIdStatuses.Voided)
                .Set(item => item.VoidReason, why)
                .Set(item => item.VoidedBy, who)
                .Set(item => item.VoidedAtUtc, clock.GetUtcNow().UtcDateTime),
            cancellationToken: cancellationToken);
        return result.ModifiedCount;
    }

    private Task LogVoidAsync(string who, string why, string releaseId, string line, CancellationToken cancellationToken) =>
        audit.AppendApplicationAuditEventAsync(
            VoidedEvent,
            who,
            "Team",
            "battery-ids",
            line,
            new BsonDocument { ["releaseId"] = releaseId, ["reason"] = why },
            entityType: "dpp-battery-ids",
            entityId: releaseId,
            cancellationToken: cancellationToken);

    /// <summary>Every battery's serial number and battery ID, by their letters and digits, naming the battery.</summary>
    private async Task<Dictionary<string, string>> BatteriesByKeyAsync(CancellationToken cancellationToken)
    {
        var byKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pack in await store.ListAsync<BatteryPack>(cancellationToken))
        {
            var serial = Text(pack, "batterySerialNumber");
            var name = serial ?? pack.Id;
            if (serial is not null)
            {
                byKey.TryAdd(IdentifierForms.Clean(serial), name);
            }

            if (Text(pack, "uniqueProductIdentifier") is { } identifier && IdentifierForms.Short(identifier) is var item && item != identifier)
            {
                byKey.TryAdd(item, name);
            }
        }

        return byKey;
    }

    private static string? Text(BatteryPack pack, string key) =>
        pack.Values.GetValueOrDefault(key)?.Value is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string? Reason(string? reason) => reason?.Trim() is { Length: > 0 and <= MaxReason } why ? why : null;

    private static string Span(IReadOnlyList<ReleasedBatteryId> records) =>
        records.Count == 1 ? records[0].SerialNumber : $"{records[0].SerialNumber} … {records[^1].SerialNumber}";

    private static string Words(int count, string one, string many) => $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? one : many)}";

    private static ReleaseResult Refused(string problem) => new(0, null, [], problem);

    [GeneratedRegex("^(?<prefix>.*?)(?<number>[0-9]+)$")]
    private static partial Regex Numbered();
}
