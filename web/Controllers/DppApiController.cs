using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The external API on the new model (decision 29, steps 3c-1 and 3c-2). A token reads as a reader
/// (<see cref="DppReaders.For"/>) and writes as a sender (<see cref="DppReaders.SenderFor"/>): a cluster token like a user
/// of its clusters and as a battery system that feeds their live values (J-10), a global token like the team. Every
/// passport answer is cut by the same rule as the pages, so a battery outside the token's clusters gives public data.
/// </summary>
[ApiController]
[Route("api/external/v1")]
public sealed class DppApiController(ExternalApiRepository tokens, ClusterRepository clusters, PassportApi api, BatteryApi writing, UploadService uploads) : ControllerBase
{
    public const string NoPassport = "No published battery passport has this ID.";
    public const string NoBattery = "No battery this token may change has this ID.";
    public const string NotYours = "This token cannot change this battery: it belongs to another cluster.";
    public const string Ambiguous = "More than one battery has this serial number; use the battery ID.";
    public const string TeamOnlyCreate = "Only team tokens create batteries.";
    public const string TeamOnlyPublish = "Only team tokens publish passports.";
    public const string LiveValuesBody = "Send a JSON object with 'values' (live values by key, e.g. stateOfChargeSoC) and, optionally, 'measuredAt' (ISO 8601 with its offset, e.g. 2026-10-27T10:02:59Z).";
    public const string CreateBody = "Send a JSON object with batchId, operatorId, optionally clusterId, and values (batterySerialNumber is required).";

    [HttpGet("clusters")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
    public async Task<IActionResult> Clusters(CancellationToken cancellationToken)
    {
        var (reader, error) = await ReaderAsync(cancellationToken);
        if (reader is null)
        {
            return error!;
        }

        var listed = (await clusters.ListClustersAsync(cancellationToken))
            .Select(cluster => (Id: BsonHelpers.GetString(cluster, "clusterId"), Name: BsonHelpers.GetString(cluster, "name")))
            .Where(cluster => MayList(reader, cluster.Id))
            .Select(cluster => new { clusterId = cluster.Id, name = cluster.Name })
            .ToArray();
        return Envelope(StatusCodes.Status200OK, "Accessible clusters read successfully.", new
        {
            globalAccess = reader.Roles.Contains(ReaderRole.Team),
            clusters = listed,
        });
    }

    [HttpGet("clusters/{clusterId}/batteries")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
    public async Task<IActionResult> ClusterBatteries(string clusterId, CancellationToken cancellationToken)
    {
        var (reader, error) = await ReaderAsync(cancellationToken);
        if (reader is null)
        {
            return error!;
        }

        var id = ClusterRepository.NormalizeClusterId(clusterId);
        if (!MayList(reader, id))
        {
            return Envelope(StatusCodes.Status403Forbidden, "Token cannot access this cluster.");
        }

        if (await clusters.GetClusterByIdAsync(id, cancellationToken) is not { } cluster)
        {
            return Envelope(StatusCodes.Status404NotFound, "Cluster was not found.");
        }

        var rows = await api.BatteriesAsync(reader, id, cancellationToken);
        return Envelope(StatusCodes.Status200OK, "Cluster batteries read successfully.", new
        {
            clusterId = id,
            clusterName = BsonHelpers.GetString(cluster, "name"),
            batteries = rows.Select(row => new
            {
                batteryId = row.Identifier,
                serialNumber = row.SerialNumber,
                batteryFamily = row.FamilyName,
                batteryModel = row.ModelName,
                passportId = (row.Active ?? row.Latest)?.DigitalProductPassportId,
                version = (row.Active ?? row.Latest)?.Number,
                dppStatus = row.StatusName,
            }).ToArray(),
        });
    }

    [HttpGet("batteries/{id}")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
    public async Task<IActionResult> Battery(string id, CancellationToken cancellationToken)
    {
        var (passport, error) = await PassportAsync(id, cancellationToken);
        if (passport is null)
        {
            return error!;
        }

        return Envelope(StatusCodes.Status200OK, "Battery passport read successfully.", new
        {
            batteryId = passport.Location.Identifier,
            passportId = passport.Version.DigitalProductPassportId,
            version = passport.Version.Number,
            dppStatus = passport.Version.DppStatus,
            signatureVerified = passport.SignatureVerified,
            passport = passport.Passport,
        });
    }

    [HttpGet("batteries/{id}/passports")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
    public async Task<IActionResult> Passports(string id, CancellationToken cancellationToken)
    {
        var (passport, error) = await PassportAsync(id, cancellationToken);
        if (passport is null)
        {
            return error!;
        }

        var versions = await api.VersionsAsync(passport, cancellationToken);
        return Envelope(StatusCodes.Status200OK, "Battery passport versions read successfully.", new
        {
            batteryId = passport.Location.Identifier,
            passports = versions.Select(version => new
            {
                passportId = version.DigitalProductPassportId,
                version = version.Number,
                dppStatus = version.DppStatus,
                publishedAt = PassportApi.Timestamp(version.PublishedAtUtc),
                lastUpdated = PassportApi.Timestamp(version.LastUpdatedUtc),
                signatureVerified = PassportReader.Verify(version),
            }).ToArray(),
        });
    }

    [HttpGet("batteries/{id}/section/{sectionName}")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
    public async Task<IActionResult> Section(string id, string sectionName, CancellationToken cancellationToken)
    {
        if (PassportApi.SectionNamed(sectionName) is not { } section)
        {
            return Envelope(StatusCodes.Status400BadRequest, $"Unknown section '{sectionName}'. Sections: {string.Join(", ", PassportApi.Sections)}.");
        }

        var (passport, error) = await PassportAsync(id, cancellationToken);
        if (passport is null)
        {
            return error!;
        }

        return Envelope(StatusCodes.Status200OK, "Section read successfully.", new
        {
            batteryId = passport.Location.Identifier,
            passportId = passport.Version.DigitalProductPassportId,
            section,
            value = PassportApi.Section(passport, section),
        });
    }

    [HttpGet("batteries/{id}/values")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
    public async Task<IActionResult> Values(string id, [FromQuery(Name = "path")] string[] paths, CancellationToken cancellationToken)
    {
        if (!paths.Any(path => !string.IsNullOrWhiteSpace(path)))
        {
            return Envelope(StatusCodes.Status400BadRequest, "At least one 'path' query parameter is required, e.g. path=stateOfChargeSoC or path=%2363.");
        }

        var (passport, error) = await PassportAsync(id, cancellationToken);
        if (passport is null)
        {
            return error!;
        }

        return Envelope(StatusCodes.Status200OK, "Values read successfully.", new
        {
            batteryId = passport.Location.Identifier,
            passportId = passport.Version.DigitalProductPassportId,
            values = PassportApi.Values(passport, paths).Select(value => new
            {
                request = value.Request,
                attribute = value.Attribute?.Label,
                key = value.Attribute?.Property,
                name = value.Attribute?.Name,
                unit = value.Attribute?.Unit,
                exists = value.Value is not null,
                value = value.Value,
                generatedAt = value.Origin is null ? null : PassportApi.Timestamp(value.Origin.GeneratedAtUtc),
                source = value.Origin is null ? null : PassportPages.SourceWord(value.Origin.Source),
                message = value.Attribute is null
                    ? "Not an attribute of the passport."
                    : value.Value is null ? "No value this token may see." : null,
            }).ToArray(),
        });
    }

    [HttpGet("batteries/{id}/paths")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
    public async Task<IActionResult> Paths(string id, [FromQuery] string? section, CancellationToken cancellationToken)
    {
        var wanted = string.IsNullOrWhiteSpace(section) || section.Trim().Equals("full", StringComparison.OrdinalIgnoreCase)
            ? null
            : PassportApi.SectionNamed(section);
        if (!string.IsNullOrWhiteSpace(section) && !section.Trim().Equals("full", StringComparison.OrdinalIgnoreCase) && wanted is null)
        {
            return Envelope(StatusCodes.Status400BadRequest, $"Unknown section '{section}'. Sections: {string.Join(", ", PassportApi.Sections)}.");
        }

        var (passport, error) = await PassportAsync(id, cancellationToken);
        if (passport is null)
        {
            return error!;
        }

        return Envelope(StatusCodes.Status200OK, "Readable values listed successfully.", new
        {
            batteryId = passport.Location.Identifier,
            passportId = passport.Version.DigitalProductPassportId,
            section = wanted ?? "full",
            attributes = PassportApi.Readable(passport, wanted).Select(attribute => new
            {
                attribute = attribute.Label,
                key = attribute.Property,
                name = attribute.Name,
                section = attribute.Section,
                path = attribute.Path,
                unit = attribute.Unit,
            }).ToArray(),
        });
    }

    [HttpGet("batteries/{id}/live-values/history")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
    public async Task<IActionResult> LiveValueHistory(string id, [FromQuery] int? hours, CancellationToken cancellationToken)
    {
        var span = hours ?? BatteryApi.MaxHistoryHours;
        if (span is < 1 or > BatteryApi.MaxHistoryHours)
        {
            return Envelope(StatusCodes.Status400BadRequest, $"hours must be between 1 and {BatteryApi.MaxHistoryHours}.");
        }

        var (reader, error) = await ReaderAsync(cancellationToken);
        if (reader is null)
        {
            return error!;
        }

        var passport = await api.ReadAsync(reader, id, cancellationToken);
        if (passport is null)
        {
            return Envelope(StatusCodes.Status404NotFound, NoPassport);
        }

        var history = await writing.HistoryAsync(reader, passport.Version.PackId, span, cancellationToken);
        return Envelope(StatusCodes.Status200OK, "Live value history read successfully.", new
        {
            batteryId = passport.Location.Identifier,
            hours = span,
            values = history.Select(entry => new
            {
                attribute = entry.Attribute.Label,
                key = entry.Attribute.Property,
                name = entry.Attribute.Name,
                unit = entry.Attribute.Unit,
                value = entry.Value,
                measuredAt = PassportApi.Timestamp(entry.MeasuredAtUtc),
                savedAt = PassportApi.Timestamp(entry.SavedAtUtc),
                source = PassportPages.SourceWord(entry.Source),
            }).ToArray(),
        });
    }

    [HttpPost("batteries/{id}/live-values")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiWrite)]
    public async Task<IActionResult> WriteLiveValues(string id, [FromBody] JsonElement body, [FromQuery] bool preview, CancellationToken cancellationToken)
    {
        if (body.ValueKind != JsonValueKind.Object
            || !body.TryGetProperty("values", out var valuesElement)
            || valuesElement.ValueKind != JsonValueKind.Object
            || !valuesElement.EnumerateObject().Any())
        {
            return Envelope(StatusCodes.Status400BadRequest, LiveValuesBody);
        }

        DateTime? measuredAt = null;
        if (body.TryGetProperty("measuredAt", out var measuredElement) && measuredElement.ValueKind != JsonValueKind.Null)
        {
            if (measuredElement.ValueKind != JsonValueKind.String
                || !DateTimeOffset.TryParse(measuredElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment))
            {
                return Envelope(StatusCodes.Status400BadRequest, LiveValuesBody);
            }

            measuredAt = moment.UtcDateTime;
            if (writing.InTheFuture(measuredAt.Value))
            {
                return Envelope(StatusCodes.Status400BadRequest, "measuredAt lies in the future.");
            }
        }

        var (token, error) = await TokenAsync(ExternalTokenRequirement.Write, cancellationToken);
        if (token is null)
        {
            return error!;
        }

        var sender = DppReaders.SenderFor(token);
        var (target, refused) = await TargetAsync(sender, id, cancellationToken);
        if (target is null)
        {
            return refused!;
        }

        var result = await writing.WriteLiveValuesAsync(sender, target.Pack!, Values(valuesElement), measuredAt, preview, cancellationToken);
        var accepted = result.Keys.Any(key => key.Outcome == KeyOutcome.Saved);
        return Envelope(
            accepted ? StatusCodes.Status200OK : StatusCodes.Status422UnprocessableEntity,
            !accepted ? "No value was saved; see each key." : preview ? "Preview only: nothing was saved." : "Live values saved.",
            new
            {
                batteryId = target.Identifier,
                preview,
                saved = result.Saved,
                measuredAt = measuredAt is null ? null : PassportApi.Timestamp(measuredAt.Value),
                values = Keys(result.Keys),
            });
    }

    /// <summary>
    /// An upload of many batteries (design 8.1, steps 5a and 5b): the native format or the battery system's export
    /// (mapping BMS2_ACO), through the one save pipeline. A cluster
    /// token sends as its battery system, so only live values are saved (J-10); a global token sends as the team.
    /// </summary>
    [HttpPost("uploads")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiWrite)]
    [RequestSizeLimit(UploadService.MaxBytes)]
    public async Task<IActionResult> Upload([FromBody] JsonElement body, [FromQuery] bool preview, CancellationToken cancellationToken)
    {
        var (token, error) = await TokenAsync(ExternalTokenRequirement.Write, cancellationToken);
        if (token is null)
        {
            return error!;
        }

        var report = await uploads.RunAsync(DppReaders.SenderFor(token), JsonNode.Parse(body.GetRawText()), "api", null, preview, cancellationToken);
        if (report.Problem is not null)
        {
            return Envelope(StatusCodes.Status400BadRequest, $"The file can't be read: {report.Problem}.");
        }

        return Envelope(
            report.AnySaved ? StatusCodes.Status200OK : StatusCodes.Status422UnprocessableEntity,
            !report.AnySaved ? "No value was saved; see each battery." : preview ? "Preview only: nothing was saved." : "Upload saved.",
            new
            {
                uploadId = report.UploadId,
                format = report.Format,
                preview,
                saved = !preview && report.AnySaved,
                batteries = report.Batteries.Select(battery => new
                {
                    entry = battery.Entry,
                    namedAs = battery.NamedAs,
                    batteryId = battery.BatteryId,
                    outcome = battery.Outcome,
                    problem = battery.Problem,
                    values = Keys(battery.Values),
                    modules = battery.Modules,
                    notes = battery.Notes ?? [],
                }).ToArray(),
            });
    }

    [HttpGet("batches")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
    public async Task<IActionResult> Batches(CancellationToken cancellationToken)
    {
        var (token, error) = await TeamTokenAsync(ExternalTokenRequirement.Read, TeamOnlyCreate, cancellationToken);
        if (token is null)
        {
            return error!;
        }

        var choices = await writing.ChoicesAsync(cancellationToken);
        return Envelope(StatusCodes.Status200OK, "Batches read successfully.", new
        {
            batches = choices.Batches.Select(batch => new
            {
                batchId = batch.BatchId,
                batteryFamily = batch.FamilyName,
                batteryModel = batch.ModelName,
                year = batch.Year,
                manufacturingSite = batch.SiteName,
            }).ToArray(),
        });
    }

    [HttpGet("operators")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
    public async Task<IActionResult> Operators(CancellationToken cancellationToken)
    {
        var (token, error) = await TeamTokenAsync(ExternalTokenRequirement.Read, TeamOnlyCreate, cancellationToken);
        if (token is null)
        {
            return error!;
        }

        var choices = await writing.ChoicesAsync(cancellationToken);
        return Envelope(StatusCodes.Status200OK, "Economic operators read successfully.", new
        {
            operators = choices.Operators.Select(item => new { operatorId = item.Id, name = item.Name }).ToArray(),
        });
    }

    [HttpPost("batteries")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiWrite)]
    public async Task<IActionResult> CreateBattery([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var (token, error) = await TeamTokenAsync(ExternalTokenRequirement.Write, TeamOnlyCreate, cancellationToken);
        if (token is null)
        {
            return error!;
        }

        if (body.ValueKind != JsonValueKind.Object)
        {
            return Envelope(StatusCodes.Status400BadRequest, CreateBody);
        }

        var values = body.TryGetProperty("values", out var valuesElement) && valuesElement.ValueKind == JsonValueKind.Object
            ? Values(valuesElement)
            : new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        var result = await writing.CreateAsync(DppReaders.SenderFor(token), Text(body, "batchId"), Text(body, "operatorId"), Text(body, "clusterId"), values, cancellationToken);
        if (result.Pack is not { } pack)
        {
            return Envelope(StatusCodes.Status400BadRequest, $"The battery was not created: {result.Problem}.", new { values = Keys(result.Keys) });
        }

        var batteryId = pack.Values["uniqueProductIdentifier"].Value!.GetValue<string>();
        return Envelope(StatusCodes.Status201Created, "Draft battery created.", new
        {
            batteryId,
            serialNumber = pack.Values["batterySerialNumber"].Value!.GetValue<string>(),
            clusterId = pack.ClusterId,
            dppStatus = "draft",
            values = Keys(result.Keys),
            publishPath = $"/api/external/v1/batteries/{Uri.EscapeDataString(batteryId)}/publish",
        });
    }

    [HttpPost("batteries/{id}/publish")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiLifecycle)]
    public async Task<IActionResult> Publish(string id, CancellationToken cancellationToken)
    {
        var (token, error) = await TeamTokenAsync(ExternalTokenRequirement.Sign, TeamOnlyPublish, cancellationToken);
        if (token is null)
        {
            return error!;
        }

        var sender = DppReaders.SenderFor(token);
        var (target, refused) = await TargetAsync(sender, id, cancellationToken);
        if (target is null)
        {
            return refused!;
        }

        var result = await writing.PublishAsync(sender, target.Pack!, cancellationToken);
        if (result.Version is not { } version)
        {
            return Envelope(StatusCodes.Status409Conflict, $"The passport was not published: {result.Problem}.", new
            {
                batteryId = target.Identifier,
                blocking = result.Report?.Blocking ?? [],
                warnings = result.Report?.Warnings ?? [],
            });
        }

        return Envelope(StatusCodes.Status200OK, result.Unchanged ? "Nothing changed since the current version, which stays as it is." : $"Version {version.Number} published.", new
        {
            batteryId = target.Identifier,
            passportId = version.DigitalProductPassportId,
            version = version.Number,
            dppStatus = version.DppStatus,
            unchanged = result.Unchanged,
            warnings = result.Report?.Warnings ?? [],
        });
    }

    /// <summary>The token, when it has the access the call needs; on failure the answer to send (401, 403 or 503, as before).</summary>
    private async Task<(ExternalApiTokenContext? Token, IActionResult? Error)> TokenAsync(ExternalTokenRequirement requirement, CancellationToken cancellationToken)
    {
        if (!tokens.IsAvailable)
        {
            return (null, Envelope(StatusCodes.Status503ServiceUnavailable, "Database is not connected."));
        }

        var validation = await tokens.ValidateTokenAsync(ExternalApiCredentials.Token(Request), requirement, cancellationToken);
        return validation.Success && validation.Context is not null
            ? (validation.Context, null)
            : (null, Envelope(validation.StatusCode, validation.Message));
    }

    /// <summary>A global (team) token with the access the call needs: creating and publishing are the team's (design 6.1).</summary>
    private async Task<(ExternalApiTokenContext? Token, IActionResult? Error)> TeamTokenAsync(ExternalTokenRequirement requirement, string refusal, CancellationToken cancellationToken)
    {
        var (token, error) = await TokenAsync(requirement, cancellationToken);
        return token is null ? (null, error)
            : token.GlobalAccess ? (token, null)
            : (null, Envelope(StatusCodes.Status403Forbidden, refusal));
    }

    /// <summary>The battery a write names, for this sender; on failure the answer to send (404, 403 or 409).</summary>
    private async Task<(WriteTarget? Target, IActionResult? Error)> TargetAsync(ChangeSender sender, string id, CancellationToken cancellationToken)
    {
        var target = await writing.FindAsync(sender, id, cancellationToken);
        return target.Refusal switch
        {
            WriteRefusal.None => (target, null),
            WriteRefusal.NotYours => (null, Envelope(StatusCodes.Status403Forbidden, NotYours)),
            WriteRefusal.Ambiguous => (null, Envelope(StatusCodes.Status409Conflict, Ambiguous)),
            _ => (null, Envelope(StatusCodes.Status404NotFound, NoBattery)),
        };
    }

    /// <summary>The token's reader; on failure the answer to send (401, 403 or 503, as before).</summary>
    private async Task<(Reader? Reader, IActionResult? Error)> ReaderAsync(CancellationToken cancellationToken)
    {
        var (token, error) = await TokenAsync(ExternalTokenRequirement.Read, cancellationToken);
        return token is null ? (null, error) : (DppReaders.For(token), null);
    }

    /// <summary>What happened to each key, in the order sent: saved, kept (empty), ignored or rejected, and why.</summary>
    private static object[] Keys(IEnumerable<KeyResult> keys) => keys.Select(key => (object)new
    {
        key = key.Key,
        attribute = AttributeCatalog.FindByProperty(key.Key)?.Label,
        outcome = key.Outcome switch
        {
            KeyOutcome.Saved => "saved",
            KeyOutcome.KeptEmpty => "kept",
            KeyOutcome.Ignored => "ignored",
            KeyOutcome.Removed => "removed",
            _ => "rejected",
        },
        message = string.IsNullOrEmpty(key.Message) ? null : key.Message,
    }).ToArray();

    private static Dictionary<string, JsonNode?> Values(JsonElement values) =>
        values.EnumerateObject().ToDictionary(property => property.Name, property => JsonNode.Parse(property.Value.GetRawText()), StringComparer.Ordinal);

    private static string? Text(JsonElement body, string name) =>
        body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private async Task<(ApiPassport? Passport, IActionResult? Error)> PassportAsync(string id, CancellationToken cancellationToken)
    {
        var (reader, error) = await ReaderAsync(cancellationToken);
        if (reader is null)
        {
            return (null, error);
        }

        var passport = await api.ReadAsync(reader, id, cancellationToken);
        return passport is null ? (null, Envelope(StatusCodes.Status404NotFound, NoPassport)) : (passport, null);
    }

    /// <summary>A cluster's batteries are listed to the team and to the cluster's own tokens.</summary>
    private static bool MayList(Reader reader, string clusterId) =>
        reader.Roles.Contains(ReaderRole.Team) || reader.ClusterIds.Contains(ClusterRepository.NormalizeClusterId(clusterId));

    private ObjectResult Envelope(int statusCode, string message, object? data = null) =>
        StatusCode(statusCode, new { success = statusCode is >= 200 and < 300, code = statusCode, message, data });
}
