using System.Text;
using System.Text.Json;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace BatteryPassWeb.Controllers;

[ApiController]
[Route("api/external/v1")]
public class ExternalApiController : ControllerBase
{
    private const string UnknownBatteryModelMessage = "Unknown Battery Model.";

    private static readonly IReadOnlyDictionary<string, string> SectionPathByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["general"] = "aspects.generalProductInformation.payload",
        ["materialcomposition"] = "aspects.materialComposition.payload",
        ["performance"] = "aspects.performanceAndDurability.payload",
        ["circularity"] = "aspects.circularity.payload",
        ["supplychain"] = "aspects.supplyChainDueDiligence.payload",
        ["carbonfootprint"] = "aspects.carbonFootprintForBatteries.payload",
        ["compliance"] = "aspects.labeling.payload",
        ["operations"] = "app.operations",
        ["display"] = "app.display",
        ["full"] = string.Empty
    };

    private static readonly IReadOnlyDictionary<string, string> ParameterAliasPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ratedEnergy"] = "aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedEnergy",
        ["ratedCapacity"] = "aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedCapacity",
        ["ratedMaximumPower"] = "aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedMaximumPower",
        ["nominalVoltage"] = "aspects.performanceAndDurability.payload.batteryTechicalProperties.nominalVoltage",
        ["nickelMass"] = "@nickelMass",
        ["currentConsumptionKwh"] = "app.operations.latestTelemetry.currentConsumptionKwh",
        ["currentChargeLevelPct"] = "app.operations.latestTelemetry.currentChargeLevelPct",
        ["currentVoltageV"] = "app.operations.latestTelemetry.currentVoltageV",
        ["currentCurrentA"] = "app.operations.latestTelemetry.currentCurrentA",
        ["softwareVersion"] = "app.product.softwareVersion",
        ["softwareReleaseDate"] = "app.product.softwareReleaseDate",
        ["softwareLatestUpdate"] = "app.product.softwareLatestUpdate",
        ["locationOfUse"] = "app.operations.locationOfUse",
        ["contactPerson"] = "app.operations.contactPerson",
        ["isActive"] = "app.operations.isActive"
    };

    private readonly PassportRepository _passportRepository;
    private readonly BatteryRepository _batteryRepository;
    private readonly ExternalApiRepository _externalApiRepository;
    private readonly BatteryTelemetryRepository _batteryTelemetryRepository;
    private readonly BatteryPassportSnapshotService _batteryPassportSnapshotService;
    private readonly BatteryTemplateUpdateService _batteryTemplateUpdateService;
    private readonly PassportTrustWorkflowService _passportTrustWorkflowService;

    public ExternalApiController(
        PassportRepository passportRepository,
        BatteryRepository batteryRepository,
        ExternalApiRepository externalApiRepository,
        BatteryTelemetryRepository batteryTelemetryRepository,
        BatteryPassportSnapshotService batteryPassportSnapshotService,
        BatteryTemplateUpdateService batteryTemplateUpdateService,
        PassportTrustWorkflowService passportTrustWorkflowService)
    {
        _passportRepository = passportRepository;
        _batteryRepository = batteryRepository;
        _externalApiRepository = externalApiRepository;
        _batteryTelemetryRepository = batteryTelemetryRepository;
        _batteryPassportSnapshotService = batteryPassportSnapshotService;
        _batteryTemplateUpdateService = batteryTemplateUpdateService;
        _passportTrustWorkflowService = passportTrustWorkflowService;
    }

    [HttpGet("batteries/{batteryId}")]
    public async Task<IActionResult> GetBattery(string batteryId, CancellationToken cancellationToken)
    {
        var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Read, cancellationToken);
        if (auth.ErrorResult != null)
        {
            return auth.ErrorResult;
        }

        var passport = await GetLatestPassportForBatteryAsync(batteryId, cancellationToken);
        if (passport == null)
        {
            return Envelope(StatusCodes.Status404NotFound, "No passport snapshot exists for this battery.");
        }

        return Envelope(StatusCodes.Status200OK, "Battery data read successfully.", new
        {
            batteryId,
            passportId = BsonHelpers.GetString(passport, "passportId"),
            battery = BsonHelpers.ToDotNet(passport)
        });
    }

    [HttpGet("batteries/{batteryId}/section/{sectionName}")]
    public async Task<IActionResult> GetBatterySection(string batteryId, string sectionName, CancellationToken cancellationToken)
    {
        var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Read, cancellationToken);
        if (auth.ErrorResult != null)
        {
            return auth.ErrorResult;
        }
        var passport = await GetLatestPassportForBatteryAsync(batteryId, cancellationToken);
        if (passport == null)
        {
            return Envelope(StatusCodes.Status404NotFound, "No passport snapshot exists for this battery.");
        }

        if (!SectionPathByName.TryGetValue(sectionName, out var sectionPath))
        {
            return Envelope(StatusCodes.Status400BadRequest, $"Unknown section '{sectionName}'.");
        }

        if (string.IsNullOrWhiteSpace(sectionPath))
        {
            return Envelope(StatusCodes.Status200OK, "Section read successfully.", new
            {
                batteryId,
                passportId = BsonHelpers.GetString(passport, "passportId"),
                section = sectionName,
                value = BsonHelpers.ToDotNet(passport)
            });
        }

        var sectionValue = ResolvePathValue(passport, sectionPath);
        if (sectionValue == null)
        {
            return Envelope(StatusCodes.Status404NotFound, $"Section '{sectionName}' does not exist for this battery.");
        }

        return Envelope(StatusCodes.Status200OK, "Section read successfully.", new
        {
            batteryId,
            passportId = BsonHelpers.GetString(passport, "passportId"),
            section = sectionName,
            path = sectionPath,
            value = BsonHelpers.ToDotNet(sectionValue)
        });
    }

    [HttpGet("batteries/{batteryId}/values")]
    public async Task<IActionResult> GetBatteryValues(string batteryId, [FromQuery(Name = "path")] string[] paths, CancellationToken cancellationToken)
    {
        var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Read, cancellationToken);
        if (auth.ErrorResult != null)
        {
            return auth.ErrorResult;
        }
        var passport = await GetLatestPassportForBatteryAsync(batteryId, cancellationToken);
        if (passport == null)
        {
            return Envelope(StatusCodes.Status404NotFound, "No passport snapshot exists for this battery.");
        }

        if (paths.Length == 0)
        {
            return Envelope(StatusCodes.Status400BadRequest, "At least one 'path' query parameter is required.");
        }

        var results = new List<object>();
        foreach (var rawPath in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var resolvedPath = ResolveParameterPath(rawPath.Trim());
            var value = ResolvePathValue(passport, resolvedPath);
            results.Add(new
            {
                requestPath = rawPath,
                resolvedPath,
                exists = value != null,
                value = BsonHelpers.ToDotNet(value)
            });
        }

        return Envelope(StatusCodes.Status200OK, "Values read successfully.", new
        {
            batteryId,
            passportId = BsonHelpers.GetString(passport, "passportId"),
            values = results
        });
    }

    [HttpGet("batteries/{batteryId}/paths")]
    public async Task<IActionResult> GetBatteryPaths(
        string batteryId,
        [FromQuery] string? section,
        [FromQuery] bool includeContainers,
        CancellationToken cancellationToken)
    {
        var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Read, cancellationToken);
        if (auth.ErrorResult != null)
        {
            return auth.ErrorResult;
        }
        var passport = await GetLatestPassportForBatteryAsync(batteryId, cancellationToken);
        if (passport == null)
        {
            return Envelope(StatusCodes.Status404NotFound, "No passport snapshot exists for this battery.");
        }

        var requestedSection = string.IsNullOrWhiteSpace(section) ? "full" : section.Trim();
        BsonValue rootValue;
        string rootPath;
        if (requestedSection.Equals("full", StringComparison.OrdinalIgnoreCase))
        {
            rootValue = passport;
            rootPath = string.Empty;
        }
        else
        {
            if (!SectionPathByName.TryGetValue(requestedSection, out var sectionPath))
            {
                return Envelope(StatusCodes.Status400BadRequest, $"Unknown section '{requestedSection}'.");
            }

            rootPath = sectionPath;
            rootValue = ResolvePathValue(passport, sectionPath) ?? BsonNull.Value;
            if (rootValue.IsBsonNull)
            {
                return Envelope(StatusCodes.Status404NotFound, $"Section '{requestedSection}' does not exist for this battery.");
            }
        }

        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectReadablePaths(rootValue, rootPath, includeContainers, paths);

        var aliases = ParameterAliasPath
            .Where(item => IsAliasRelevantForSection(item.Value, requestedSection))
            .Select(item =>
            {
                var value = ResolvePathValue(passport, item.Value);
                return new
                {
                    alias = item.Key,
                    path = item.Value,
                    exists = value != null
                };
            })
            .ToList();

        return Envelope(StatusCodes.Status200OK, "Readable paths resolved successfully.", new
        {
            batteryId,
            passportId = BsonHelpers.GetString(passport, "passportId"),
            section = requestedSection,
            rootPath = string.IsNullOrWhiteSpace(rootPath) ? "(full document)" : rootPath,
            includeContainers,
            aliases,
            paths
        });
    }

    [HttpPost("batteries/{batteryId}/telemetry")]
    public async Task<IActionResult> WriteTelemetry(string batteryId, [FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Write, cancellationToken);
        if (auth.ErrorResult != null)
        {
            return auth.ErrorResult;
        }

        if (!TryParseTelemetryPoints(payload, out var points, out var telemetryError))
        {
            return Envelope(StatusCodes.Status400BadRequest, telemetryError);
        }

        await _batteryTelemetryRepository.AppendTelemetryAsync(batteryId, points, cancellationToken);

        var latestPoint = points.OrderByDescending(point => point.MeasuredAtUtc).First();
        var setValues = new Dictionary<string, BsonValue>
        {
            ["app.operations.latestTelemetry.measuredAt"] = latestPoint.MeasuredAtUtc.ToString("O"),
            ["app.operations.lastUpdatedAt"] = DateTime.UtcNow.ToString("O")
        };

        if (latestPoint.CurrentConsumptionKwh.HasValue)
        {
            setValues["app.operations.latestTelemetry.currentConsumptionKwh"] = latestPoint.CurrentConsumptionKwh.Value;
        }
        if (latestPoint.CurrentChargeLevelPct.HasValue)
        {
            setValues["app.operations.latestTelemetry.currentChargeLevelPct"] = latestPoint.CurrentChargeLevelPct.Value;
        }
        if (latestPoint.CurrentVoltageV.HasValue)
        {
            setValues["app.operations.latestTelemetry.currentVoltageV"] = latestPoint.CurrentVoltageV.Value;
        }
        if (latestPoint.CurrentCurrentA.HasValue)
        {
            setValues["app.operations.latestTelemetry.currentCurrentA"] = latestPoint.CurrentCurrentA.Value;
        }

        var latestPassport = await GetLatestPassportForBatteryAsync(batteryId, cancellationToken);
        var latestPassportId = latestPassport == null ? string.Empty : BsonHelpers.GetString(latestPassport, "passportId");
        if (!string.IsNullOrWhiteSpace(latestPassportId))
        {
            await _passportRepository.UpdateFieldsAsync(latestPassportId, setValues, cancellationToken);
        }

        return Envelope(StatusCodes.Status201Created, "Telemetry written successfully.", new
        {
            batteryId,
            passportId = latestPassportId,
            pointsAccepted = points.Count,
            latestMeasuredAt = latestPoint.MeasuredAtUtc.ToString("O")
        });
    }

    [HttpGet("batteries/{batteryId}/telemetry/history")]
    public async Task<IActionResult> ReadTelemetryHistory(string batteryId, [FromQuery] int? hours, CancellationToken cancellationToken)
    {
        var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Read, cancellationToken);
        if (auth.ErrorResult != null)
        {
            return auth.ErrorResult;
        }

        var effectiveHours = hours ?? 168;
        if (effectiveHours < 1 || effectiveHours > 168)
        {
            return Envelope(StatusCodes.Status400BadRequest, "hours must be between 1 and 168.");
        }

        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddHours(-effectiveHours);
        var history = await _batteryTelemetryRepository.ReadHistoryAsync(batteryId, fromUtc, toUtc, cancellationToken);

        var points = history.Select(row => new
        {
            measuredAt = BsonHelpers.GetString(row, "measuredAt"),
            currentConsumptionKwh = ReadNullableNumber(row, "currentConsumptionKwh"),
            currentChargeLevelPct = ReadNullableNumber(row, "currentChargeLevelPct"),
            currentVoltageV = ReadNullableNumber(row, "currentVoltageV"),
            currentCurrentA = ReadNullableNumber(row, "currentCurrentA")
        }).ToList();

        return Envelope(StatusCodes.Status200OK, "Telemetry history read successfully.", new
        {
            batteryId,
            hours = effectiveHours,
            points
        });
    }

    [HttpPatch("batteries/{batteryId}/operations")]
    public async Task<IActionResult> UpdateOperations(string batteryId, [FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Write, cancellationToken);
        if (auth.ErrorResult != null)
        {
            return auth.ErrorResult;
        }

        if (payload.ValueKind != JsonValueKind.Object)
        {
            return Envelope(StatusCodes.Status400BadRequest, "Body must be a JSON object.");
        }

        var setValues = new Dictionary<string, BsonValue>();
        if (TryGetPropertyIgnoreCase(payload, "isActive", out var isActiveElement))
        {
            if (isActiveElement.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            {
                return Envelope(StatusCodes.Status400BadRequest, "isActive must be a boolean.");
            }

            setValues["app.operations.isActive"] = isActiveElement.GetBoolean();
        }

        if (TryGetPropertyIgnoreCase(payload, "locationOfUse", out var locationElement))
        {
            if (locationElement.ValueKind != JsonValueKind.Object)
            {
                return Envelope(StatusCodes.Status400BadRequest, "locationOfUse must be an object.");
            }

            AddOptionalString(locationElement, "siteName", "app.operations.locationOfUse.siteName", setValues);
            AddOptionalString(locationElement, "address", "app.operations.locationOfUse.address", setValues);
            AddOptionalString(locationElement, "city", "app.operations.locationOfUse.city", setValues);
            AddOptionalString(locationElement, "country", "app.operations.locationOfUse.country", setValues);
            AddOptionalNumber(locationElement, "latitude", "app.operations.locationOfUse.latitude", setValues);
            AddOptionalNumber(locationElement, "longitude", "app.operations.locationOfUse.longitude", setValues);
        }

        if (TryGetPropertyIgnoreCase(payload, "contactPerson", out var contactElement))
        {
            if (contactElement.ValueKind != JsonValueKind.Object)
            {
                return Envelope(StatusCodes.Status400BadRequest, "contactPerson must be an object.");
            }

            AddOptionalString(contactElement, "name", "app.operations.contactPerson.name", setValues);
            AddOptionalString(contactElement, "email", "app.operations.contactPerson.email", setValues);
            AddOptionalString(contactElement, "phone", "app.operations.contactPerson.phone", setValues);
        }

        if (setValues.Count == 0)
        {
            return Envelope(StatusCodes.Status400BadRequest, "No writable operations fields were provided.");
        }

        setValues["app.operations.lastUpdatedAt"] = DateTime.UtcNow.ToString("O");
        setValues["updatedAt"] = DateTime.UtcNow.ToString("O");
        await _batteryRepository.UpdateBatteryFieldsAsync(batteryId, setValues, cancellationToken);
        var latestPassport = await GetLatestPassportForBatteryAsync(batteryId, cancellationToken);
        var latestPassportId = latestPassport == null ? string.Empty : BsonHelpers.GetString(latestPassport, "passportId");
        if (!string.IsNullOrWhiteSpace(latestPassportId))
        {
            await _passportRepository.UpdateFieldsAsync(latestPassportId, setValues, cancellationToken);
        }

        return Envelope(StatusCodes.Status200OK, "Operations fields updated successfully.", new { batteryId, passportId = latestPassportId });
    }

    [HttpPatch("batteries/{batteryId}/battery-model")]
    public async Task<IActionResult> UpdateBatteryModel(string batteryId, [FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Write, cancellationToken);
        if (auth.ErrorResult != null)
        {
            return auth.ErrorResult;
        }

        if (payload.ValueKind != JsonValueKind.Object
            || !TryGetPropertyIgnoreCase(payload, "batteryModel", out var batteryModelElement)
            || batteryModelElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(batteryModelElement.GetString()))
        {
            return Envelope(StatusCodes.Status400BadRequest, "batteryModel is required and must be a string.");
        }

        var requestedBatteryModel = batteryModelElement.GetString()!.Trim();
        var result = await _batteryTemplateUpdateService.ApplyBatteryModelAsync(auth.Battery!, requestedBatteryModel, cancellationToken);
        if (!result.Success)
        {
            return Envelope(StatusCodes.Status400BadRequest, UnknownBatteryModelMessage);
        }

        return Envelope(StatusCodes.Status200OK, "Battery Model updated on the battery record. Create, validate, sign, and publish a new passport to expose the updated snapshot.", new
        {
            batteryId,
            batteryModel = requestedBatteryModel,
            newPassportRequired = BatteryRequiresNewPassport(result.Battery)
        });
    }

    [HttpPatch("batteries/{batteryId}/software-version")]
    public async Task<IActionResult> UpdateSoftwareVersion(string batteryId, [FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Write, cancellationToken);
        if (auth.ErrorResult != null)
        {
            return auth.ErrorResult;
        }

        if (payload.ValueKind != JsonValueKind.Object
            || !TryGetPropertyIgnoreCase(payload, "softwareVersion", out var softwareVersionElement)
            || softwareVersionElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(softwareVersionElement.GetString()))
        {
            return Envelope(StatusCodes.Status400BadRequest, "softwareVersion is required and must be a string.");
        }

        var result = await _batteryTemplateUpdateService.ApplySoftwareVersionAsync(
            auth.Battery!,
            softwareVersionElement.GetString()!,
            cancellationToken);
        return Envelope(
            result.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest,
            result.Message,
            new
            {
                batteryId,
                newPassportRequired = BatteryRequiresNewPassport(result.Battery)
            });
    }

    [HttpPost("batteries/{batteryId}/passports")]
    public async Task<IActionResult> CreateBatteryPassport(string batteryId, CancellationToken cancellationToken)
    {
        var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Sign, cancellationToken);
        if (auth.ErrorResult != null)
        {
            return auth.ErrorResult;
        }

        var passport = await _batteryPassportSnapshotService.CreatePassportSnapshotAsync(
            auth.Battery!,
            auth.TokenContext!.Name,
            DateTimeOffset.UtcNow,
            cancellationToken);
        var passportId = BsonHelpers.GetString(passport, "passportId");
        return Envelope(StatusCodes.Status201Created, "Passport snapshot created.", new { batteryId, passportId });
    }

    [HttpPost("passports/{passportId}/validate")]
    public async Task<IActionResult> ValidateBatteryPassport(string passportId, CancellationToken cancellationToken)
    {
        var auth = await AuthorizeAsync(passportId, ExternalTokenRequirement.Sign, cancellationToken);
        if (auth.ErrorResult != null)
        {
            return auth.ErrorResult;
        }

        var result = await _passportTrustWorkflowService.ValidateAsync(passportId, auth.TokenContext!.Name, "external-api", cancellationToken);

        return Envelope(result.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest, result.Message, new
        {
            passportId,
            blockingErrors = result.ValidationSummary?.BlockingErrorCount ?? 0,
            warnings = result.ValidationSummary?.WarningCount ?? 0,
            canSign = result.ValidationSummary?.CanSign ?? false
        });
    }

    [HttpPost("passports/{passportId}/sign")]
    public async Task<IActionResult> SignBatteryPassport(string passportId, CancellationToken cancellationToken)
    {
        var auth = await AuthorizeAsync(passportId, ExternalTokenRequirement.Sign, cancellationToken);
        if (auth.ErrorResult != null)
        {
            return auth.ErrorResult;
        }

        var result = await _passportTrustWorkflowService.SignAsync(passportId, auth.TokenContext!.Name, "external-api", cancellationToken);

        return Envelope(result.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest, result.Message, new
        {
            passportId,
            revisionId = result.RevisionId,
            autoPublished = result.AutoPublished,
            passportStatus = result.AutoPublished ? "Published" : "Signed",
            blockingErrors = result.ValidationSummary?.BlockingErrorCount ?? 0,
            warnings = result.ValidationSummary?.WarningCount ?? 0
        });
    }

    [HttpPost("passports/{passportId}/publish")]
    public async Task<IActionResult> PublishBatteryPassport(string passportId, CancellationToken cancellationToken)
    {
        var auth = await AuthorizeAsync(passportId, ExternalTokenRequirement.Sign, cancellationToken);
        if (auth.ErrorResult != null)
        {
            return auth.ErrorResult;
        }

        var result = await _passportTrustWorkflowService.PublishAsync(passportId, auth.TokenContext!.Name, "external-api.publish", cancellationToken);
        return Envelope(result.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest, result.Message, new
        {
            passportId,
            revisionId = result.RevisionId,
            passportStatus = result.Success ? "Published" : "Signed",
            blockingErrors = result.ValidationSummary?.BlockingErrorCount ?? 0,
            warnings = result.ValidationSummary?.WarningCount ?? 0
        });
    }

    private async Task<BsonDocument?> GetLatestPassportForBatteryAsync(string batteryId, CancellationToken cancellationToken)
    {
        return (await _passportRepository.ListByBatteryIdAsync(batteryId, includeArchived: false, cancellationToken)).FirstOrDefault();
    }

    private async Task<ExternalAuthResult> AuthorizeBatteryAsync(string batteryId, ExternalTokenRequirement requirement, CancellationToken cancellationToken)
    {
        if (!_externalApiRepository.IsAvailable || !_batteryTelemetryRepository.IsAvailable)
        {
            return new ExternalAuthResult
            {
                ErrorResult = Envelope(StatusCodes.Status503ServiceUnavailable, "Database is not connected.")
            };
        }

        var tokenValidation = await ValidateTokenForRequirementAsync(requirement, cancellationToken);
        if (!tokenValidation.Success || tokenValidation.Context == null)
        {
            return new ExternalAuthResult
            {
                ErrorResult = Envelope(tokenValidation.StatusCode, tokenValidation.Message)
            };
        }

        var battery = await _batteryRepository.GetByBatteryIdAsync(batteryId, cancellationToken);
        if (battery == null)
        {
            return await RejectPassportIdForBatteryRouteAsync(batteryId, cancellationToken);
        }

        if (!CanAccessCluster(tokenValidation.Context, BsonHelpers.GetString(battery, "clusterId")))
        {
            return new ExternalAuthResult
            {
                ErrorResult = Envelope(StatusCodes.Status403Forbidden, "Token cannot access this battery cluster scope.")
            };
        }

        return new ExternalAuthResult
        {
            Battery = battery,
            TokenContext = tokenValidation.Context
        };
    }

    private async Task<ExternalAuthResult> RejectPassportIdForBatteryRouteAsync(string batteryId, CancellationToken cancellationToken)
    {
        if (await _passportRepository.GetByPassportIdAsync(batteryId, cancellationToken) != null)
        {
            return new ExternalAuthResult
            {
                ErrorResult = Envelope(StatusCodes.Status400BadRequest, "Telemetry and battery operations are keyed by Battery ID. Use the linked Battery ID, not Passport ID.")
            };
        }

        return new ExternalAuthResult
        {
            ErrorResult = Envelope(StatusCodes.Status404NotFound, "Battery was not found.")
        };
    }

    private async Task<ExternalAuthResult> AuthorizeAsync(string passportId, ExternalTokenRequirement requirement, CancellationToken cancellationToken)
    {
        if (!_externalApiRepository.IsAvailable || !_batteryTelemetryRepository.IsAvailable)
        {
            return new ExternalAuthResult
            {
                ErrorResult = Envelope(StatusCodes.Status503ServiceUnavailable, "Database is not connected.")
            };
        }

        var tokenValidation = await ValidateTokenForRequirementAsync(requirement, cancellationToken);
        if (!tokenValidation.Success || tokenValidation.Context == null)
        {
            return new ExternalAuthResult
            {
                ErrorResult = Envelope(tokenValidation.StatusCode, tokenValidation.Message)
            };
        }

        var passport = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (passport == null)
        {
            return new ExternalAuthResult
            {
                ErrorResult = Envelope(StatusCodes.Status404NotFound, "Battery passport was not found.")
            };
        }

        var clusterId = BsonHelpers.GetString(passport, "clusterId");
        if (!CanAccessCluster(tokenValidation.Context, clusterId))
        {
            return new ExternalAuthResult
            {
                ErrorResult = Envelope(StatusCodes.Status403Forbidden, "Token cannot access this battery cluster scope.")
            };
        }

        return new ExternalAuthResult
        {
            Passport = passport,
            TokenContext = tokenValidation.Context
        };
    }

    private Task<ExternalApiTokenValidationResult> ValidateTokenForRequirementAsync(
        ExternalTokenRequirement requirement,
        CancellationToken cancellationToken)
    {
        var token = ExtractToken();
        return _externalApiRepository.ValidateTokenAsync(token, requirement, cancellationToken);
    }

    private string ExtractToken()
    {
        var authorization = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return string.Empty;
        }

        const string prefix = "Basic ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var raw = authorization[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        if (TryDecodeBasicToken(raw, out var decodedToken))
        {
            return decodedToken;
        }

        // Compatibility mode: treat the Basic payload as raw token value.
        return raw;
    }

    private static bool CanAccessCluster(ExternalApiTokenContext tokenContext, string clusterId)
    {
        if (tokenContext.GlobalAccess)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(clusterId))
        {
            return tokenContext.AllowUnassigned;
        }

        return tokenContext.ClusterIds.Contains(clusterId, StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryDecodeBasicToken(string raw, out string token)
    {
        token = string.Empty;
        try
        {
            var decodedBytes = Convert.FromBase64String(raw);
            if (decodedBytes.Length == 0 || decodedBytes.Length > 512)
            {
                return false;
            }

            var decoded = Encoding.UTF8.GetString(decodedBytes);
            if (decoded.Length == 0 || decoded.Any(character => character is < ' ' or > '~'))
            {
                return false;
            }

            var separator = decoded.IndexOf(':');
            if (separator <= 0)
            {
                return false;
            }

            var candidate = decoded[..separator].Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            token = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveParameterPath(string pathOrAlias)
    {
        if (ParameterAliasPath.TryGetValue(pathOrAlias, out var resolvedPath))
        {
            return resolvedPath;
        }

        return pathOrAlias;
    }

    private static bool IsAliasRelevantForSection(string aliasPath, string section)
    {
        if (section.Equals("full", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (section.Equals("materialComposition", StringComparison.OrdinalIgnoreCase))
        {
            return aliasPath.StartsWith("aspects.materialComposition.", StringComparison.OrdinalIgnoreCase)
                || aliasPath.Equals("@nickelMass", StringComparison.OrdinalIgnoreCase);
        }

        if (!SectionPathByName.TryGetValue(section, out var sectionPath))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(sectionPath))
        {
            return true;
        }

        return aliasPath.StartsWith(sectionPath + ".", StringComparison.OrdinalIgnoreCase)
            || aliasPath.Equals(sectionPath, StringComparison.OrdinalIgnoreCase);
    }

    private static BsonValue? ResolvePathValue(BsonDocument root, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return root;
        }

        if (path.Equals("@nickelMass", StringComparison.OrdinalIgnoreCase))
        {
            var materialPayload = ResolvePathValue(root, "aspects.materialComposition.payload.batteryMaterials");
            if (materialPayload is BsonArray rows)
            {
                var nickel = rows
                    .OfType<BsonDocument>()
                    .FirstOrDefault(row =>
                    {
                        var materialName = row.GetValue("batteryMaterialName", string.Empty).ToString() ?? string.Empty;
                        return materialName.Equals("Nickel", StringComparison.OrdinalIgnoreCase);
                    });
                if (nickel != null)
                {
                    return nickel.GetValue("batteryMaterialMass", BsonNull.Value);
                }
            }

            return null;
        }

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        BsonValue current = root;
        foreach (var segment in segments)
        {
            if (current is BsonDocument document)
            {
                if (!document.TryGetValue(segment, out current))
                {
                    return null;
                }
                continue;
            }

            if (current is BsonArray array)
            {
                if (!int.TryParse(segment, out var index) || index < 0 || index >= array.Count)
                {
                    return null;
                }

                current = array[index];
                continue;
            }

            return null;
        }

        return current;
    }

    private static void CollectReadablePaths(BsonValue value, string currentPath, bool includeContainers, ISet<string> paths)
    {
        if (value is BsonDocument document)
        {
            if (includeContainers && !string.IsNullOrWhiteSpace(currentPath))
            {
                paths.Add(currentPath);
            }

            foreach (var element in document.Elements)
            {
                var nextPath = string.IsNullOrWhiteSpace(currentPath)
                    ? element.Name
                    : $"{currentPath}.{element.Name}";
                CollectReadablePaths(element.Value, nextPath, includeContainers, paths);
            }

            return;
        }

        if (value is BsonArray array)
        {
            if (includeContainers && !string.IsNullOrWhiteSpace(currentPath))
            {
                paths.Add(currentPath);
            }

            for (var index = 0; index < array.Count; index++)
            {
                var nextPath = string.IsNullOrWhiteSpace(currentPath)
                    ? index.ToString()
                    : $"{currentPath}.{index}";
                CollectReadablePaths(array[index], nextPath, includeContainers, paths);
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            paths.Add(currentPath);
        }
    }

    private IActionResult Envelope(int statusCode, string message, object? data = null)
    {
        return StatusCode(statusCode, new
        {
            success = statusCode is >= 200 and < 300,
            code = statusCode,
            message,
            data
        });
    }

    private static bool TryParseTelemetryPoints(JsonElement payload, out List<TelemetryWritePoint> points, out string error)
    {
        points = [];
        error = string.Empty;

        if (payload.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(payload, "points", out var pointsElement))
        {
            if (pointsElement.ValueKind != JsonValueKind.Array)
            {
                error = "points must be an array when provided.";
                return false;
            }

            foreach (var item in pointsElement.EnumerateArray())
            {
                if (!TryParseTelemetryPoint(item, out var point, out error))
                {
                    return false;
                }

                points.Add(point);
            }
        }
        else if (payload.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in payload.EnumerateArray())
            {
                if (!TryParseTelemetryPoint(item, out var point, out error))
                {
                    return false;
                }

                points.Add(point);
            }
        }
        else if (payload.ValueKind == JsonValueKind.Object)
        {
            if (!TryParseTelemetryPoint(payload, out var point, out error))
            {
                return false;
            }

            points.Add(point);
        }
        else
        {
            error = "Telemetry payload must be an object or array.";
            return false;
        }

        if (points.Count == 0)
        {
            error = "At least one telemetry point is required.";
            return false;
        }

        return true;
    }

    private static bool TryParseTelemetryPoint(JsonElement element, out TelemetryWritePoint point, out string error)
    {
        error = string.Empty;
        point = new TelemetryWritePoint
        {
            MeasuredAtUtc = DateTime.UtcNow
        };

        if (element.ValueKind != JsonValueKind.Object)
        {
            error = "Each telemetry point must be an object.";
            return false;
        }

        var currentConsumption = ReadNullableNumber(element, "currentConsumptionKwh");
        var currentChargeLevel = ReadNullableNumber(element, "currentChargeLevelPct");
        var currentVoltage = ReadNullableNumber(element, "currentVoltageV");
        var currentCurrent = ReadNullableNumber(element, "currentCurrentA");

        if (!currentConsumption.HasValue
            && !currentChargeLevel.HasValue
            && !currentVoltage.HasValue
            && !currentCurrent.HasValue)
        {
            error = "Each telemetry point must include at least one writable telemetry field.";
            return false;
        }

        DateTime measuredAtUtc = DateTime.UtcNow;
        if (TryGetPropertyIgnoreCase(element, "measuredAt", out var measuredAtElement))
        {
            if (measuredAtElement.ValueKind != JsonValueKind.String
                || !DateTime.TryParse(measuredAtElement.GetString(), out measuredAtUtc))
            {
                error = "measuredAt must be a valid ISO date-time string when provided.";
                return false;
            }

            measuredAtUtc = measuredAtUtc.ToUniversalTime();
        }

        point = new TelemetryWritePoint
        {
            CurrentConsumptionKwh = currentConsumption,
            CurrentChargeLevelPct = currentChargeLevel,
            CurrentVoltageV = currentVoltage,
            CurrentCurrentA = currentCurrent,
            MeasuredAtUtc = measuredAtUtc
        };

        return true;
    }

    private static double? ReadNullableNumber(BsonDocument document, string key)
    {
        var value = document.GetValue(key, BsonNull.Value);
        if (value.IsBsonNull)
        {
            return null;
        }

        return value.IsNumeric ? value.ToDouble() : null;
    }

    private static double? ReadNullableNumber(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetDouble(out var value))
        {
            return null;
        }

        return value;
    }

    private static void AddOptionalString(JsonElement source, string sourceProperty, string targetPath, IDictionary<string, BsonValue> setValues)
    {
        if (!TryGetPropertyIgnoreCase(source, sourceProperty, out var property))
        {
            return;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            setValues[targetPath] = BsonNull.Value;
            return;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            setValues[targetPath] = property.GetString() ?? string.Empty;
        }
    }

    private static void AddOptionalNumber(JsonElement source, string sourceProperty, string targetPath, IDictionary<string, BsonValue> setValues)
    {
        if (!TryGetPropertyIgnoreCase(source, sourceProperty, out var property))
        {
            return;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            setValues[targetPath] = BsonNull.Value;
            return;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value))
        {
            setValues[targetPath] = value;
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement propertyValue)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            propertyValue = default;
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                propertyValue = property.Value;
                return true;
            }
        }

        propertyValue = default;
        return false;
    }

    private static bool BatteryRequiresNewPassport(BsonDocument battery) =>
        BsonHelpers.GetValue(battery, "app", "snapshot", "newPassportRequired") is { IsBoolean: true } value && value.AsBoolean;

    private sealed class ExternalAuthResult
    {
        public IActionResult? ErrorResult { get; init; }
        public BsonDocument? Battery { get; init; }
        public BsonDocument? Passport { get; init; }
        public ExternalApiTokenContext? TokenContext { get; init; }
    }
}
