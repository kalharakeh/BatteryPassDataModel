using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The external API's reading side on the new model (decision 29, step 3c-1). A token is read as a reader
/// (<see cref="DppReaders.For"/>): a cluster token like a user of its clusters, a global token like the team. Every
/// passport answer is cut by the same rule as the pages, so a battery outside the token's clusters gives public data.
/// </summary>
[ApiController]
[Route("api/external/v1")]
public sealed class DppApiController(ExternalApiRepository tokens, ClusterRepository clusters, PassportApi api) : ControllerBase
{
    public const string NoPassport = "No published battery passport has this ID.";

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
                passportId = row.Active?.DigitalProductPassportId,
                version = row.Active?.Number,
                dppStatus = row.Active?.DppStatus ?? "draft",
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

    /// <summary>The token's reader; on failure the answer to send (401, 403 or 503, as before).</summary>
    private async Task<(Reader? Reader, IActionResult? Error)> ReaderAsync(CancellationToken cancellationToken)
    {
        if (!tokens.IsAvailable)
        {
            return (null, Envelope(StatusCodes.Status503ServiceUnavailable, "Database is not connected."));
        }

        var validation = await tokens.ValidateTokenAsync(ExternalApiCredentials.Token(Request), ExternalTokenRequirement.Read, cancellationToken);
        return validation.Success && validation.Context is not null
            ? (DppReaders.For(validation.Context), null)
            : (null, Envelope(validation.StatusCode, validation.Message));
    }

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
