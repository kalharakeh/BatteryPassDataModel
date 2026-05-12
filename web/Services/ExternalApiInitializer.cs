using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class ExternalApiInitializer
{
    public const string SamplePassportId = "did:web:acme.battery.pass:sample-customer-north-001";
    public const string SampleApiClusterId = "cluster-north-operations";
    public const string SampleReadTokenId = "sample-read-token";
    public const string SampleReadWriteTokenId = "sample-read-write-token";
    public const string SampleSignTokenId = "sample-sign-token";
    public const string SampleReadTokenValue = "SAMPLEBATTERYPASSPORTREADTOKN001";
    public const string SampleReadWriteTokenValue = "SAMPLEBATTERYPASSPORTWRITETOK001";
    public const string SampleSignTokenValue = "SAMPLEBATTERYPASSPORTSIGNTOK001";

    private readonly ExternalApiRepository _externalApiRepository;
    private readonly PassportRepository _passportRepository;
    private readonly BatteryRepository _batteryRepository;
    private readonly BatteryTelemetryRepository _batteryTelemetryRepository;
    private readonly ClusterRepository _clusterRepository;
    private readonly PassportDataNormalizationService _passportDataNormalizationService;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public ExternalApiInitializer(
        ExternalApiRepository externalApiRepository,
        PassportRepository passportRepository,
        BatteryRepository batteryRepository,
        BatteryTelemetryRepository batteryTelemetryRepository,
        ClusterRepository clusterRepository,
        PassportDataNormalizationService passportDataNormalizationService)
    {
        _externalApiRepository = externalApiRepository;
        _passportRepository = passportRepository;
        _batteryRepository = batteryRepository;
        _batteryTelemetryRepository = batteryTelemetryRepository;
        _clusterRepository = clusterRepository;
        _passportDataNormalizationService = passportDataNormalizationService;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await _externalApiRepository.EnsureIndexesAsync(cancellationToken);
            await _batteryRepository.EnsureIndexesAsync(cancellationToken);
            await _batteryTelemetryRepository.EnsureIndexesAsync(cancellationToken);
            await EnsureSamplePassportAsync(cancellationToken);
            await EnsureBatteryImagesAndCategoriesAsync(cancellationToken);
            await NormalizeExistingPassportDataAsync(cancellationToken);
            await EnsureSampleTokensAsync(cancellationToken);
            await EnsureClusterTokensAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureSamplePassportAsync(CancellationToken cancellationToken)
    {
        var existing = await _passportRepository.GetByPassportIdAsync(SamplePassportId, cancellationToken);
        if (existing == null)
        {
            return;
        }

        var now = DateTime.UtcNow.ToString("O");
        var registryInfo = EnsureDocument(existing, "registryInfo");
        registryInfo["status"] = "published";
        registryInfo["updatedAt"] = now;
        if (!registryInfo.Contains("registryId"))
        {
            registryInfo["registryId"] = Guid.NewGuid().ToString("N");
        }

        var operationsDoc = EnsureDocument(EnsureDocument(existing, "app"), "operations");
        operationsDoc["isActive"] = true;
        operationsDoc["lastUpdatedAt"] = now;
        if (!operationsDoc.Contains("locationOfUse"))
        {
            operationsDoc["locationOfUse"] = new BsonDocument
            {
                ["siteName"] = "Sample Site",
                ["address"] = "Example street 1",
                ["city"] = "Sample City",
                ["country"] = "DE"
            };
        }
        if (!operationsDoc.Contains("contactPerson"))
        {
            operationsDoc["contactPerson"] = new BsonDocument
            {
                ["name"] = "Sample Contact",
                ["email"] = "sample@example.test",
                ["phone"] = "+49 000 0000"
            };
        }

        await _passportRepository.ReplaceAsync(SamplePassportId, existing, cancellationToken);
    }

    private async Task EnsureSampleTokensAsync(CancellationToken cancellationToken)
    {
        await EnsureFixedSampleTokenAsync(
            SampleReadTokenId,
            SampleReadTokenValue,
            "Sample token (read)",
            ExternalTokenAccessMode.Read,
            cancellationToken);

        await EnsureFixedSampleTokenAsync(
            SampleReadWriteTokenId,
            SampleReadWriteTokenValue,
            "Sample token (read-write)",
            ExternalTokenAccessMode.ReadWrite,
            cancellationToken);

        await EnsureFixedSampleTokenAsync(
            SampleSignTokenId,
            SampleSignTokenValue,
            "Sample token (validate, sign, publish)",
            ExternalTokenAccessMode.Sign,
            cancellationToken);
    }

    private async Task EnsureFixedSampleTokenAsync(
        string tokenId,
        string tokenValue,
        string name,
        ExternalTokenAccessMode accessMode,
        CancellationToken cancellationToken)
    {
        await _externalApiRepository.UpsertFixedTokenAsync(
            tokenId,
            tokenValue,
            name,
            accessMode,
            [SampleApiClusterId],
            allowUnassigned: false,
            globalAccess: false,
            actor: "system",
            isSample: true,
            cancellationToken);
    }

    private async Task NormalizeExistingPassportDataAsync(CancellationToken cancellationToken)
    {
        var passports = await _passportRepository.SearchDocumentsAsync(string.Empty, includeArchived: true, cancellationToken);
        if (passports.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow.ToString("O");
        foreach (var passport in passports)
        {
            var passportId = BsonHelpers.GetString(passport, "passportId");
            if (string.IsNullOrWhiteSpace(passportId))
            {
                continue;
            }

            var before = passport.ToJson();
            var normalized = _passportDataNormalizationService.Normalize(passport, now);
            if (string.Equals(before, normalized.ToJson(), StringComparison.Ordinal))
            {
                continue;
            }

            normalized.Remove("_id");
            await _passportRepository.ReplaceAsync(passportId, normalized, cancellationToken);
        }
    }

    private async Task EnsureBatteryImagesAndCategoriesAsync(CancellationToken cancellationToken)
    {
        var passports = await _passportRepository.SearchDocumentsAsync(string.Empty, includeArchived: true, cancellationToken);
        if (passports.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow.ToString("O");
        foreach (var passport in passports)
        {
            var passportId = BsonHelpers.GetString(passport, "passportId");
            if (string.IsNullOrWhiteSpace(passportId))
            {
                continue;
            }

            var app = EnsureDocument(passport, "app");
            var media = EnsureDocument(app, "media");
            var aspects = EnsureDocument(passport, "aspects");
            var generalProductInformation = EnsureDocument(aspects, "generalProductInformation");
            var generalPayload = EnsureDocument(generalProductInformation, "payload");

            var currentImageUrl = BatteryImageCatalog.NormalizeAssetUrl(media.GetValue("batteryImageUrl", string.Empty).ToString());
            var selectedImageUrl = BatteryImageCatalog.NormalizeKnownImageUrl(currentImageUrl, passportId);
            var currentCategory = BsonHelpers.GetString(generalPayload, "batteryCategory");
            var selectedCategory = BatteryPassCanonicalDataCatalog.NormalizeBatteryCategory(currentCategory);

            var hasChanges = false;
            if (!string.Equals(currentImageUrl, selectedImageUrl, StringComparison.OrdinalIgnoreCase))
            {
                media["batteryImageUrl"] = selectedImageUrl;
                hasChanges = true;
            }

            if (!string.Equals(currentCategory, selectedCategory, StringComparison.OrdinalIgnoreCase))
            {
                generalPayload["batteryCategory"] = selectedCategory;
                hasChanges = true;
            }

            if (!hasChanges)
            {
                continue;
            }

            var registryInfo = EnsureDocument(passport, "registryInfo");
            registryInfo["updatedAt"] = now;
            passport.Remove("_id");
            await _passportRepository.ReplaceAsync(passportId, passport, cancellationToken);
        }
    }

    private async Task EnsureClusterTokensAsync(CancellationToken cancellationToken)
    {
        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        if (clusters.Count == 0)
        {
            return;
        }

        var existingTokens = await _externalApiRepository.ListTokensAsync(cancellationToken);
        foreach (var cluster in clusters)
        {
            var clusterId = BsonHelpers.GetString(cluster, "clusterId");
            var clusterName = BsonHelpers.GetString(cluster, "name");
            if (string.IsNullOrWhiteSpace(clusterId))
            {
                continue;
            }

            var hasReadToken = existingTokens.Any(token =>
                BsonHelpers.GetString(token, "autoClusterId").Equals(clusterId, StringComparison.OrdinalIgnoreCase)
                && BsonHelpers.GetString(token, "accessMode").Equals("read", StringComparison.OrdinalIgnoreCase));
            if (!hasReadToken)
            {
                await _externalApiRepository.CreateTokenAsync(
                    $"{clusterName} - read",
                    ExternalTokenAccessMode.Read,
                    [clusterId],
                    allowUnassigned: false,
                    globalAccess: false,
                    actor: "system",
                    autoClusterId: clusterId,
                    cancellationToken: cancellationToken);
            }

            var hasReadWriteToken = existingTokens.Any(token =>
                BsonHelpers.GetString(token, "autoClusterId").Equals(clusterId, StringComparison.OrdinalIgnoreCase)
                && BsonHelpers.GetString(token, "accessMode").Equals("readWrite", StringComparison.OrdinalIgnoreCase));
            if (!hasReadWriteToken)
            {
                await _externalApiRepository.CreateTokenAsync(
                    $"{clusterName} - readwrite",
                    ExternalTokenAccessMode.ReadWrite,
                    [clusterId],
                    allowUnassigned: false,
                    globalAccess: false,
                    actor: "system",
                    autoClusterId: clusterId,
                    cancellationToken: cancellationToken);
            }
        }
    }

    private static BsonDocument BuildFallbackSampleDocument()
    {
        var now = DateTime.UtcNow.ToString("O");
        return new BsonDocument
        {
            ["passportId"] = SamplePassportId,
            ["registryInfo"] = new BsonDocument
            {
                ["registryId"] = Guid.NewGuid().ToString("N"),
                ["status"] = "published",
                ["createdAt"] = now,
                ["updatedAt"] = now
            },
            ["app"] = new BsonDocument
            {
                ["display"] = new BsonDocument
                {
                    ["name"] = "Sample Battery",
                    ["modelNumber"] = "SAMPLE-001",
                    ["serialNumber"] = "SN-SAMPLE-001",
                    ["manufacturerName"] = "Scania Industrial Batteries"
                },
                ["media"] = new BsonDocument
                {
                    ["batteryImageUrl"] = BatteryImageCatalog.DefaultImageUrl
                }
            },
            ["aspects"] = new BsonDocument
            {
                ["generalProductInformation"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryCategory"] = BatteryPassCanonicalDataCatalog.DemoBatteryCategory
                    }
                }
            }
        };
    }

    private static BsonDocument EnsureDocument(BsonDocument parent, string key)
    {
        if (!parent.TryGetValue(key, out var value) || value is not BsonDocument document)
        {
            document = new BsonDocument();
            parent[key] = document;
        }

        return document;
    }
}
