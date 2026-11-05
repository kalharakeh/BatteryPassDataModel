using System.Globalization;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace BatteryPassWeb.Dpp.Audit;

/// <summary>The small grey icon of an event's kind; only refusals are drawn red.</summary>
public enum AuditIcon
{
    Edit,
    Add,
    Remove,
    Upload,
    Publish,
    Lock,
    Key,
    Image,
    Alert,
    Other,
}

/// <summary>A piece of an event's sentence; strong pieces are the names of people and records.</summary>
public sealed record AuditWord(string Text, bool Strong = false);

/// <summary>One fact in an event's details, with a link when it names a record that still exists.</summary>
public sealed record AuditFact(string Term, string Detail, string? Href = null);

/// <summary>One value of a change: what it was, what it became, and whether it was saved.</summary>
public sealed record AuditValueRow(string Name, string Before, string After, string Result, bool Refused);

/// <summary>An audit event as one line in plain English, with its details for when the line is opened.</summary>
public sealed record AuditLine(
    string Id,
    string EventType,
    DateTimeOffset? When,
    AuditIcon Icon,
    bool IsProblem,
    IReadOnlyList<AuditWord> Sentence,
    string? Change,
    IReadOnlyList<string> Tags,
    IReadOnlyList<AuditFact> Facts,
    IReadOnlyList<AuditValueRow> Values,
    string Technical)
{
    public string Text => string.Concat(Sentence.Select(word => word.Text));
}

/// <summary>The events of one day, newest first.</summary>
public sealed record AuditDay(string Title, IReadOnlyList<AuditLine> Lines);

/// <summary>
/// Turns audit events into plain English for the audit log: "Kassem changed the battery mass of model Universal",
/// with each value before and after. Record names come from the records themselves (or, for removed ones, from what
/// the event recorded), attribute names from the catalogue, and people's names from their accounts.
/// </summary>
public sealed class AuditLines(DppStore store, DocumentStore documents, AttributeForms forms, MongoContext context)
{
    private const int ShortChange = 60;

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>The time zone the page shows times in: the server's (on the team's PC, their own).</summary>
    public TimeZoneInfo Zone { get; init; } = TimeZoneInfo.Local;

    public async Task<IReadOnlyList<AuditLine>> DescribeAsync(IReadOnlyList<BsonDocument> events, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        var records = await RecordNames.LoadAsync(store, documents, events, cancellationToken);
        var people = await PeopleAsync(events, cancellationToken);
        return events.Select(auditEvent => Describe(auditEvent, records, people)).ToArray();
    }

    /// <summary>Groups lines (newest first) by day, naming today and yesterday.</summary>
    public static IReadOnlyList<AuditDay> ByDay(IReadOnlyList<AuditLine> lines, TimeZoneInfo zone, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        return lines
            .GroupBy(line => line.When is { } when ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(when, zone).DateTime) : (DateOnly?)null)
            .Select(day => new AuditDay(DayTitle(day.Key, today), day.ToArray()))
            .ToArray();
    }

    private static string DayTitle(DateOnly? day, DateOnly today)
    {
        if (day is not { } date)
        {
            return "Date unknown";
        }

        var name = date.ToString("dddd d MMMM yyyy", English);
        return date == today ? $"Today · {name}" : date == today.AddDays(-1) ? $"Yesterday · {name}" : name;
    }

    private AuditLine Describe(BsonDocument auditEvent, RecordNames records, IReadOnlyDictionary<string, string> people)
    {
        var eventType = BsonHelpers.GetString(auditEvent, "eventType");
        var email = BsonHelpers.GetString(auditEvent, "actor");
        var role = FirstNonEmpty(BsonHelpers.GetString(auditEvent, "actorRole"), BsonHelpers.GetString(auditEvent, "actorType"));
        // A token is named by what it is, never by its ID, as in a battery's earlier uploads (step 6c).
        var actor = email.StartsWith("api-token:", StringComparison.Ordinal)
            ? (role == nameof(SenderKind.BatterySystemFeed) ? "Battery system" : "API token")
            : people.GetValueOrDefault(email) ?? (email.Length > 0 ? email : "The app");
        var source = BsonHelpers.GetString(auditEvent, "source");
        var entityType = BsonHelpers.GetString(auditEvent, "entityType");
        var entityId = BsonHelpers.GetString(auditEvent, "entityId");
        var message = BsonHelpers.GetString(auditEvent, "message");
        var metadata = auditEvent.TryGetValue("metadata", out var value) && value.IsBsonDocument ? value.AsBsonDocument : new BsonDocument();
        var when = DateTimeOffset.TryParse(BsonHelpers.GetString(auditEvent, "createdAt"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : (DateTimeOffset?)null;
        var record = records.Find(entityType, entityId);

        var words = new List<AuditWord> { new(actor, Strong: true) };
        string? change = null;
        var icon = AuditIcon.Other;
        var problem = false;
        var rows = Array.Empty<AuditValueRow>();
        var facts = new List<AuditFact>
        {
            new("Who", email.Length > 0 ? (role.Length > 0 ? $"{email} · {role.ToLowerInvariant()}" : email) : "The app itself"),
        };
        if (when is { } time)
        {
            var local = TimeZoneInfo.ConvertTime(time, Zone);
            facts.Add(new("When", $"{local.ToString("d MMM yyyy, HH:mm:ss", English)} ({time.ToUniversalTime().ToString("HH:mm:ss", English)} UTC)"));
        }

        facts.Add(new("Came from", SourceLong(source, role)));

        switch (eventType)
        {
            case SavePipeline.AuditEventType:
                (icon, change, rows) = Change(words, metadata, record, entityType, entityId);
                break;
            case ProductDataPages.CreatedEvent:
                icon = AuditIcon.Add;
                words.Add(new($" added {KindWord(entityType)} "));
                words.Add(new(record?.Name ?? NameFrom(message, " created"), Strong: true));
                if (After(message, " as a copy of ") is { } copy)
                {
                    words.Add(new(", a copy of "));
                    words.Add(new(copy.Split(" (", 2)[0], Strong: true));
                }

                break;
            case ProductDataPages.RenamedEvent:
                icon = AuditIcon.Edit;
                var names = message.Split(" renamed to ", 2);
                words.Add(new($" renamed {KindWord(entityType)} "));
                words.Add(new(names[0], Strong: true));
                if (names.Length == 2)
                {
                    words.Add(new(" to "));
                    words.Add(new(names[1], Strong: true));
                }

                break;
            case ProductDataPages.RemovedEvent:
                icon = AuditIcon.Remove;
                words.Add(new($" removed {KindWord(entityType)} "));
                words.Add(new(record?.Name ?? NameFrom(message, " removed"), Strong: true));
                break;
            case ProductDataPages.PictureEvent:
                icon = AuditIcon.Image;
                words.Add(new(" set the picture of battery family "));
                words.Add(new(record?.Name ?? entityId, Strong: true));
                break;
            case DocumentService.UploadedEvent:
            case DocumentService.DeniedEvent:
                var document = records.Document(entityId);
                var owner = document is null ? null : records.Find(OwnerType(document.OwnerKind), document.OwnerId);
                var what = document is null
                    ? "a document"
                    : $"the {Lower(AttributeCatalog.FindByProperty(document.Property)?.Name ?? document.Property)} of {KindWord(OwnerType(document.OwnerKind))} ";
                if (eventType == DocumentService.UploadedEvent)
                {
                    icon = AuditIcon.Upload;
                    words.Add(new($" uploaded {what}"));
                    change = document is null ? null : $"{document.FileName}, {Size(document.Length)}";
                }
                else
                {
                    icon = AuditIcon.Alert;
                    problem = true;
                    words[0] = new(email.Length > 0 ? actor : "A reader without access", Strong: true);
                    words.Add(new($" was refused {what}"));
                }

                if (document is not null)
                {
                    words.Add(new(owner?.Name ?? document.OwnerId, Strong: true));
                    facts.Add(new("File", $"{document.FileName}, {Size(document.Length)}, {document.ContentType}"));
                }

                record = owner;
                break;
            case BatteryPages.ClusterChangedEvent:
                icon = AuditIcon.Edit;
                words.Add(new(" moved battery "));
                words.Add(new(record?.Name ?? entityId, Strong: true));
                words.Add(new(" to another cluster"));
                change = After(message, " moved from ") is { } move ? move.Replace(" to ", " → ", StringComparison.Ordinal) : null;
                break;
            case PackCreationService.AuditEventType:
                icon = AuditIcon.Add;
                words.Add(new(" created battery "));
                words.Add(new(record?.Name ?? entityId, Strong: true));
                break;
            case UploadService.UploadedEvent:
                icon = AuditIcon.Upload;
                var counts = message.Split(": ", 2);
                words.Add(new(" uploaded values for "));
                words.Add(new(counts[0].Replace("Upload of ", string.Empty, StringComparison.Ordinal), Strong: true));
                change = counts.Length == 2 ? counts[1] : null;
                break;
            case PublicationService.InactiveEvent:
                // An earlier passport replaced by the battery's new one (step 4c), named by the battery (step 6c).
                icon = AuditIcon.Publish;
                words.Add(new(" set the passport of battery "));
                words.Add(new(record?.Name ?? entityId, Strong: true));
                words.Add(new(" inactive"));
                change = metadata.TryGetValue("why", out var why) && why.IsString ? why.AsString : null;
                break;
            case PublicationService.PublishedEvent:
                icon = AuditIcon.Publish;
                words.Add(new(" published battery "));
                words.Add(new(record?.Name ?? entityId, Strong: true));
                change = metadata.TryGetValue("number", out var number) ? $"version {number}" : null;
                break;
            case PublicationService.BlockedEvent:
                icon = AuditIcon.Alert;
                problem = true;
                words.Clear();
                words.Add(new("A new version of battery "));
                words.Add(new(record?.Name ?? entityId, Strong: true));
                words.Add(new(" was blocked"));
                change = metadata.TryGetValue("blocking", out var blocking) && blocking is BsonArray { Count: > 0 } reasons ? Clip(reasons[0].ToString() ?? string.Empty, ShortChange) : null;
                break;
            default:
                icon = eventType.Contains("token", StringComparison.OrdinalIgnoreCase) ? AuditIcon.Key
                    : eventType.Contains("login", StringComparison.OrdinalIgnoreCase) || eventType.StartsWith("auth", StringComparison.OrdinalIgnoreCase)
                      || BsonHelpers.GetString(auditEvent, "category") == "security" ? AuditIcon.Lock
                    : AuditIcon.Other;
                problem = new[] { "fail", "denied", "refused", "blocked" }.Any(word => eventType.Contains(word, StringComparison.OrdinalIgnoreCase));
                words.Add(new($": {(message.Length > 0 ? message : eventType.Replace('.', ' ').Replace('-', ' '))}"));
                break;
        }

        if (record is not null)
        {
            facts.Add(new("Record", $"{Capital(record.Kind)} {record.Name}{(record.Context is null ? string.Empty : $" {record.Context}")}", record.Href));
        }
        else if (entityId.Length > 0)
        {
            facts.Add(new("Record", $"{KindTag(entityType)} {entityId}"));
        }

        if (metadata.TryGetValue("passportId", out var passport) && passport.IsString)
        {
            facts.Add(new("Passport", passport.AsString));
        }

        var technical = auditEvent.DeepClone().AsBsonDocument;
        technical.Remove("_id");
        var tags = new[] { KindTag(entityType), SourceTag(source, role) }.Where(tag => tag.Length > 0).Distinct().ToArray();
        return new AuditLine(
            FirstNonEmpty(BsonHelpers.GetString(auditEvent, "eventId"), Guid.NewGuid().ToString("N")),
            eventType,
            when,
            icon,
            problem,
            words,
            change is { Length: > 0 and <= ShortChange } ? change : null,
            tags,
            facts,
            rows,
            technical.ToJson(new JsonWriterSettings { Indent = true }));
    }

    /// <summary>A save: one value reads as its own sentence, several are counted; each is listed in the details.</summary>
    private (AuditIcon Icon, string? Change, AuditValueRow[] Rows) Change(List<AuditWord> words, BsonDocument metadata, RecordName? record, string entityType, string entityId)
    {
        var keys = metadata.TryGetValue("keys", out var list) && list is BsonArray array
            ? array.Where(item => item.IsBsonDocument).Select(item => item.AsBsonDocument).ToArray()
            : [];
        var saved = keys.Where(key => Outcome(key) == nameof(KeyOutcome.Saved)).ToArray();
        var removed = keys.Where(key => Outcome(key) == nameof(KeyOutcome.Removed)).ToArray();
        var refused = keys.Count(key => Outcome(key) == nameof(KeyOutcome.Rejected));
        var name = record?.Name ?? entityId;
        string? change = null;
        if (saved.Length + removed.Length == 1 && refused == 0)
        {
            var key = saved.FirstOrDefault() ?? removed[0];
            var attribute = AttributeCatalog.FindByProperty(BsonHelpers.GetString(key, "key"));
            var before = Format(key, "previous");
            var verb = removed.Length == 1 ? "removed" : before == "—" ? "filled in" : "changed";
            words.Add(new($" {verb} the {Lower(attribute?.Name ?? BsonHelpers.GetString(key, "key"))} of {KindWord(entityType)} "));
            words.Add(new(name, Strong: true));
            // Entries from before the new value was recorded (29 Oct 2026) show no short change.
            change = removed.Length == 1 ? $"was {before}"
                : !key.Contains("value") ? null
                : before == "—" ? Format(key, "value") : $"{before} → {Format(key, "value")}";
        }
        else
        {
            var parts = new List<string>();
            if (saved.Length > 0)
            {
                parts.Add($"saved {Words.Count(saved.Length, "value")}");
            }

            if (removed.Length > 0)
            {
                parts.Add($"removed {Words.Count(removed.Length, "value")}");
            }

            words.Add(new($" {(parts.Count == 0 ? "changed nothing" : string.Join(" and ", parts))} of {KindWord(entityType)} "));
            words.Add(new(name, Strong: true));
            if (refused > 0)
            {
                words.Add(new($"; {refused} {(refused == 1 ? "was" : "were")} refused"));
            }
        }

        var rows = keys.Select(key =>
        {
            var property = BsonHelpers.GetString(key, "key");
            var attribute = AttributeCatalog.FindByProperty(property);
            var label = attribute is null ? property : $"{attribute.Name} ({attribute.Label})";
            var detail = BsonHelpers.GetString(key, "message");
            return Outcome(key) switch
            {
                nameof(KeyOutcome.Saved) => new AuditValueRow(label, Format(key, "previous"), key.Contains("value") ? Format(key, "value") : "Not recorded", "Saved", false),
                nameof(KeyOutcome.Removed) => new AuditValueRow(label, Format(key, "previous"), "—", "Removed", false),
                nameof(KeyOutcome.Rejected) => new AuditValueRow(label, string.Empty, string.Empty, $"Refused: {detail}", true),
                _ => new AuditValueRow(label, string.Empty, string.Empty, detail.Length > 0 ? $"Not changed: {detail}" : "Not changed", false),
            };
        }).ToArray();
        return (removed.Length > 0 && saved.Length == 0 ? AuditIcon.Remove : AuditIcon.Edit, change, rows);
    }

    /// <summary>A value as the form would show it, e.g. "600 kg", "Rated capacity: 200 Ah; …", "2 rows"; "—" when empty.</summary>
    private string Format(BsonDocument key, string field)
    {
        if (!key.TryGetValue(field, out var raw) || !raw.IsString)
        {
            return "—";
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(raw.AsString);
        }
        catch (System.Text.Json.JsonException)
        {
            return Clip(raw.AsString, 200);
        }

        if (node is null)
        {
            return "—";
        }

        var attribute = AttributeCatalog.FindByProperty(BsonHelpers.GetString(key, "key"));
        if (attribute is null)
        {
            return Clip(node.ToJsonString(), 200);
        }

        var form = forms.For(attribute);
        // The schema's codes in words, as the pages say them ("Re-used"), and the accident reports' page by its name, not
        // its internal address (redesign R6 review).
        if (attribute.Number is 2 or 20 or 22)
        {
            return Clip(string.Join(", ", ValueWords.Lines(form, node).Select(line => line.Text)), 200);
        }

        if (attribute.Property == DocumentService.Accidents)
        {
            return "the accident reports page";
        }

        if (form.Kind is FormKind.Link or FormKind.Upload)
        {
            return Clip(Plain(node["title"]) is { Length: > 0 } title ? title : Plain(node["url"]) is { Length: > 0 } url ? url : "a document", 200);
        }

        if (form.Kind == FormKind.Rows)
        {
            return Words.Count(FormValues.RowCount(node), "row");
        }

        if (node is JsonArray items)
        {
            return Clip(string.Join(", ", items.Select(Plain)), 200);
        }

        if (form.Kind == FormKind.Fixed)
        {
            return Clip(Plain(node), 200);
        }

        var texts = FormValues.Show(form, node);
        var parts = form.Inputs
            .Select(input => (input, text: texts.GetValueOrDefault(form.FieldName(input)) ?? string.Empty))
            .Where(part => part.text.Length > 0)
            .Select(part => (part.input, text: part.input.Unit is { Length: > 0 } unit ? $"{part.text} {unit}" : part.text))
            .ToArray();
        if (parts.Length == 0)
        {
            return Clip(node.ToJsonString(), 200);
        }

        return Clip(
            form.Kind == FormKind.Single || form.Inputs.Count == 1
                ? parts[0].text
                : string.Join("; ", parts.Select(part => $"{part.input.Label}: {part.text}")),
            200);
    }

    private async Task<IReadOnlyDictionary<string, string>> PeopleAsync(IReadOnlyList<BsonDocument> events, CancellationToken cancellationToken)
    {
        var emails = events.Select(auditEvent => BsonHelpers.GetString(auditEvent, "actor")).Where(email => email.Contains('@', StringComparison.Ordinal)).Distinct().ToArray();
        if (context.Database is null || emails.Length == 0)
        {
            return new Dictionary<string, string>();
        }

        var users = await context.Database.GetCollection<BsonDocument>("users")
            .Find(Builders<BsonDocument>.Filter.In("email", emails))
            .ToListAsync(cancellationToken);
        return users
            .Select(user => (email: BsonHelpers.GetString(user, "email"), name: BsonHelpers.GetString(user, "name").Trim()))
            .Where(user => user.name.Length > 0)
            .GroupBy(user => user.email, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().name, StringComparer.Ordinal);
    }

    private static string Outcome(BsonDocument key) => BsonHelpers.GetString(key, "outcome");

    private static string Plain(JsonNode? node) => node switch
    {
        null => string.Empty,
        JsonValue text when text.TryGetValue<string>(out var value) => value,
        _ => node.ToJsonString(),
    };

    private static string KindWord(string entityType) => entityType switch
    {
        "dpp-family" => "battery family",
        "dpp-model" => "model",
        "dpp-batch" => "batch",
        "dpp-site" => "manufacturing site",
        "dpp-operator" => "economic operator",
        "dpp-pack" => "battery",
        "dpp-document" => "document",
        _ => entityType,
    };

    private static string KindTag(string entityType) => entityType switch
    {
        "dpp-family" => "Battery family",
        "dpp-model" => "Battery model",
        "dpp-batch" => "Batch",
        "dpp-site" => "Site",
        "dpp-operator" => "Operator",
        "dpp-pack" or "battery" => "Battery",
        "dpp-document" => "Document",
        "passport" => "Passport",
        "cluster" => "Cluster",
        "apiToken" => "API token",
        "account" => "Account",
        _ => Capital(entityType),
    };

    private static string SourceTag(string source, string role) => source switch
    {
        "form" or "publication" => "Team page",
        "documents" => role.Contains("cluster", StringComparison.OrdinalIgnoreCase) ? "Cluster page" : "Team page",
        "external-api" or "api" or "feed" => "API",
        "upload" => "Upload",
        "admin-ui" => "Admin page",
        _ => string.Empty,
    };

    private static string SourceLong(string source, string role) => source switch
    {
        "form" => "The team's pages",
        "publication" => "Publishing, on the team's pages",
        "documents" => role.Contains("cluster", StringComparison.OrdinalIgnoreCase) ? "A document on the cluster pages" : "A document on the team's pages",
        "external-api" or "api" or "feed" => "The API",
        "upload" => "A data upload",
        "admin-ui" => "The admin pages",
        "" => "Not recorded",
        _ => source,
    };

    private static string OwnerType(TargetKind kind) => $"dpp-{kind.ToString().ToLowerInvariant()}";

    /// <summary>The name in a recorded summary such as "Battery family Compact 7M created".</summary>
    private static string NameFrom(string message, string suffix)
    {
        var text = message.EndsWith(suffix, StringComparison.Ordinal) ? message[..^suffix.Length] : message.Split(suffix, 2)[0];
        foreach (var prefix in new[] { "Battery family ", "Battery model ", "Batch ", "Manufacturing site ", "Economic operator ", "Battery " })
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                return text[prefix.Length..];
            }
        }

        return text;
    }

    private static string? After(string text, string marker)
    {
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? null : text[(index + marker.Length)..];
    }

    /// <summary>Lower-cases a name's first letter for use inside a sentence, unless it starts an abbreviation ("EU …").</summary>
    private static string Lower(string name) =>
        name.Length > 1 && char.IsUpper(name[0]) && !char.IsUpper(name[1]) ? char.ToLowerInvariant(name[0]) + name[1..] : name;

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string Clip(string text, int length) => text.Length <= length ? text : text[..(length - 1)] + "…";

    private static string Size(long bytes) => bytes >= 1_000_000 ? $"{bytes / 1_000_000.0:0.#} MB" : bytes >= 1_000 ? $"{bytes / 1_000.0:0} kB" : $"{bytes} bytes";

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => value.Length > 0) ?? string.Empty;

    /// <summary>A record's name, where it is edited, and what it belongs to.</summary>
    private sealed record RecordName(string Kind, string Name, string? Href, string? Context);

    /// <summary>The names of the records the events are about, loaded in one go per kind.</summary>
    private sealed class RecordNames
    {
        private readonly Dictionary<(string Type, string Id), RecordName> names = new();
        private readonly Dictionary<string, DppDocument> documentsById = new(StringComparer.Ordinal);

        public RecordName? Find(string entityType, string id) => names.GetValueOrDefault((entityType, id));

        public DppDocument? Document(string id) => documentsById.GetValueOrDefault(id);

        public static async Task<RecordNames> LoadAsync(DppStore store, DocumentStore documents, IReadOnlyList<BsonDocument> events, CancellationToken cancellationToken)
        {
            var result = new RecordNames();
            var ids = events
                .Select(auditEvent => (Type: BsonHelpers.GetString(auditEvent, "entityType"), Id: BsonHelpers.GetString(auditEvent, "entityId")))
                .Where(entity => entity.Type.StartsWith("dpp-", StringComparison.Ordinal) && entity.Id.Length > 0)
                .Distinct()
                .ToList();
            foreach (var (_, id) in ids.Where(entity => entity.Type == "dpp-document").ToArray())
            {
                if (await documents.GetAsync(id, cancellationToken) is { } document)
                {
                    result.documentsById[id] = document;
                    ids.Add((OwnerType(document.OwnerKind), document.OwnerId));
                }
            }

            string[] Of(string type) => ids.Where(entity => entity.Type == type).Select(entity => entity.Id).Distinct().ToArray();
            var batches = await store.GetManyAsync<ProductionBatch>(Of("dpp-batch"), cancellationToken);
            var models = await store.GetManyAsync<BatteryModel>(Of("dpp-model").Concat(batches.Select(batch => batch.ModelId)).Distinct(), cancellationToken);
            var families = await store.GetManyAsync<BatteryFamily>(Of("dpp-family").Concat(models.Select(model => model.FamilyId)).Distinct(), cancellationToken);
            var sites = await store.GetManyAsync<ManufacturingSite>(Of("dpp-site").Concat(batches.Select(batch => batch.SiteId)).Distinct(), cancellationToken);
            var operators = await store.GetManyAsync<EconomicOperator>(Of("dpp-operator"), cancellationToken);
            var packs = await store.GetManyAsync<BatteryPack>(Of("dpp-pack"), cancellationToken);

            var familyById = families.ToDictionary(family => family.Id, StringComparer.Ordinal);
            var modelById = models.ToDictionary(model => model.Id, StringComparer.Ordinal);
            var siteById = sites.ToDictionary(site => site.Id, StringComparer.Ordinal);
            foreach (var family in families)
            {
                result.names[("dpp-family", family.Id)] = new("battery family", family.Name, ProductDataPages.FamilyHref(family.Id), null);
            }

            foreach (var model in models)
            {
                result.names[("dpp-model", model.Id)] = new(
                    "model",
                    model.Name,
                    $"{ProductDataPages.FamilyHref(model.FamilyId, model.Id)}#editor",
                    familyById.TryGetValue(model.FamilyId, out var family) ? $"of family {family.Name}" : null);
            }

            foreach (var site in sites)
            {
                result.names[("dpp-site", site.Id)] = new("manufacturing site", site.Name, $"{ProductDataPages.SitesHref("site", site.Id)}#editor", null);
            }

            foreach (var item in operators)
            {
                result.names[("dpp-operator", item.Id)] = new("economic operator", item.Name, $"{ProductDataPages.SitesHref("operator", item.Id)}#editor", null);
            }

            foreach (var batch in batches)
            {
                var site = siteById.TryGetValue(batch.SiteId, out var found) ? found.Name : batch.SiteId;
                result.names[("dpp-batch", batch.Id)] = new(
                    "batch",
                    $"{batch.Year} · {site}",
                    $"/admin/batches/{Uri.EscapeDataString(batch.Id)}#editor",
                    modelById.TryGetValue(batch.ModelId, out var model) ? $"of model {model.Name}" : null);
            }

            var serial = AttributeCatalog.ByNumber(11).Property;
            foreach (var pack in packs)
            {
                var name = pack.Values.TryGetValue(serial, out var value) && Plain(value.Value) is { Length: > 0 } text ? text : pack.Id;
                result.names[("dpp-pack", pack.Id)] = new("battery", name, BatteryPages.BatteryHref(pack.Id), null);
            }

            return result;
        }
    }
}
