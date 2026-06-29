using BatteryPassWeb.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class ExternalApiInitializer
{
    public const string SamplePassportId = "did:web:acme.battery.pass:sample-demo-passport";
    public const string LegacySampleBatteryId = "did:web:acme.battery.pass:sample-customer-north-001";
    public const string SampleApiClusterId = "demo-cluster";
    public const string SampleBatteryFamily = "Compact 7M";
    public const string SampleBatterySerialNumber = "SN-DEMO-API-001";
    public const string SampleReadTokenId = "sample-read-token";
    public const string SampleReadWriteTokenId = "sample-read-write-token";
    public const string SampleSignTokenId = "sample-sign-token";
    public const string SampleLifecycleTokenId = "sample-lifecycle-token";
    public const string SampleReadTokenValue = "SAMPLEBATTERYPASSPORTREADTOKN001";
    public const string SampleReadWriteTokenValue = "SAMPLEBATTERYPASSPORTWRITETOK001";
    public const string SampleSignTokenValue = "SAMPLEBATTERYPASSPORTSIGNTOK001";
    public const string SampleLifecycleTokenValue = "SAMPLEBATTERYPASSPORTLIFECYC001";

    public static string CreateSampleBatteryId(BatteryIdService batteryIdService)
    {
        return batteryIdService.CreateBatteryId(SampleBatteryFamily, SampleBatterySerialNumber);
    }

    public static BsonDocument CreateFallbackSampleDocument(string sampleBatteryId)
    {
        return BuildFallbackSampleDocument(sampleBatteryId);
    }

    private readonly ExternalApiRepository _externalApiRepository;
    private readonly PassportRepository _passportRepository;
    private readonly BatteryRepository _batteryRepository;
    private readonly BatteryTelemetryRepository _batteryTelemetryRepository;
    private readonly ClusterRepository _clusterRepository;
    private readonly PassportDataNormalizationService _passportDataNormalizationService;
    private readonly BatteryIdService _batteryIdService;
    private readonly BatteryPassOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public ExternalApiInitializer(
        ExternalApiRepository externalApiRepository,
        PassportRepository passportRepository,
        BatteryRepository batteryRepository,
        BatteryTelemetryRepository batteryTelemetryRepository,
        ClusterRepository clusterRepository,
        PassportDataNormalizationService passportDataNormalizationService,
        BatteryIdService batteryIdService,
        IOptions<BatteryPassOptions> options)
    {
        _externalApiRepository = externalApiRepository;
        _passportRepository = passportRepository;
        _batteryRepository = batteryRepository;
        _batteryTelemetryRepository = batteryTelemetryRepository;
        _clusterRepository = clusterRepository;
        _passportDataNormalizationService = passportDataNormalizationService;
        _batteryIdService = batteryIdService;
        _options = options.Value;
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
            await _clusterRepository.EnsureIndexesAsync(cancellationToken);
            await _batteryTelemetryRepository.EnsureIndexesAsync(cancellationToken);
            if (_options.EnableDemoData)
            {
                await EnsureSamplePassportAsync(cancellationToken);
            }
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
        var sampleBatteryId = CreateSampleBatteryId(_batteryIdService);
        var now = DateTime.UtcNow.ToString("O");
        await _clusterRepository.UpsertClusterAsync(SampleApiClusterId, "Demo API Cluster", cancellationToken);
        await _batteryRepository.DeleteByFamilyAndSerialExceptAsync(
            SampleBatteryFamily,
            SampleBatterySerialNumber,
            sampleBatteryId,
            cancellationToken);

        var existingBattery = await _batteryRepository.GetByBatteryIdAsync(sampleBatteryId, cancellationToken);
        if (existingBattery == null)
        {
            await _batteryRepository.ReplaceAsync(sampleBatteryId, BuildFallbackSampleBattery(sampleBatteryId), cancellationToken);
        }

        var sampleBatteryPassports = await _passportRepository.ListByBatteryIdAsync(sampleBatteryId, includeArchived: true, cancellationToken);
        var latestSampleBatteryPassport = sampleBatteryPassports
            .FirstOrDefault(passport => passport.GetValue("isLatestForBattery", false).ToBoolean())
            ?? sampleBatteryPassports.FirstOrDefault();
        if (latestSampleBatteryPassport != null)
        {
            await EnsureSamplePassportPublicAsync(latestSampleBatteryPassport, sampleBatteryId, now, cancellationToken);
            return;
        }

        var existing = await _passportRepository.GetByPassportIdAsync(SamplePassportId, cancellationToken)
            ?? BuildFallbackSampleDocument(sampleBatteryId);
        EnsureSamplePassportShape(existing, sampleBatteryId, now);
        await _passportRepository.ReplaceAsync(BsonHelpers.GetString(existing, "passportId"), existing, cancellationToken);
    }

    private async Task EnsureSamplePassportPublicAsync(
        BsonDocument passport,
        string sampleBatteryId,
        string now,
        CancellationToken cancellationToken)
    {
        EnsureSamplePassportShape(passport, sampleBatteryId, now);

        var passportId = BsonHelpers.GetString(passport, "passportId");
        if (!string.IsNullOrWhiteSpace(passportId))
        {
            passport.Remove("_id");
            await _passportRepository.ReplaceAsync(passportId, passport, cancellationToken);
        }
    }

    private static void EnsureSamplePassportShape(BsonDocument passport, string sampleBatteryId, string now)
    {
        if (string.IsNullOrWhiteSpace(BsonHelpers.GetString(passport, "passportId")))
        {
            passport["passportId"] = SamplePassportId;
        }

        passport["batteryId"] = sampleBatteryId;
        if (string.IsNullOrWhiteSpace(BsonHelpers.GetString(passport, "clusterId")))
        {
            passport["clusterId"] = SampleApiClusterId;
        }

        passport["isLatestForBattery"] = true;
        var registryInfo = EnsureDocument(passport, "registryInfo");
        registryInfo["status"] = "published";
        registryInfo["updatedAt"] = now;
        registryInfo["hasBeenPublished"] = true;
        if (!registryInfo.Contains("createdAt"))
        {
            registryInfo["createdAt"] = now;
        }
        if (!registryInfo.Contains("registryId"))
        {
            registryInfo["registryId"] = Guid.NewGuid().ToString("N");
        }

        var snapshot = EnsureDocument(passport, "snapshot");
        if (string.IsNullOrWhiteSpace(BsonHelpers.GetString(snapshot, "batteryFamily")))
        {
            snapshot["batteryFamily"] = SampleBatteryFamily;
        }
        if (string.IsNullOrWhiteSpace(BsonHelpers.GetString(snapshot, "batteryModel")))
        {
            snapshot["batteryModel"] = "1.0";
        }
        if (string.IsNullOrWhiteSpace(BsonHelpers.GetString(snapshot, "batterySerialNumber")))
        {
            snapshot["batterySerialNumber"] = SampleBatterySerialNumber;
        }
        if (string.IsNullOrWhiteSpace(BsonHelpers.GetString(snapshot, "createdAt")))
        {
            snapshot["createdAt"] = now;
        }

        var app = EnsureDocument(passport, "app");
        var display = EnsureDocument(app, "display");
        if (string.IsNullOrWhiteSpace(BsonHelpers.GetString(display, "name")))
        {
            display["name"] = "Demo API Compact 7M battery";
        }
        if (string.IsNullOrWhiteSpace(BsonHelpers.GetString(display, "modelNumber")))
        {
            display["modelNumber"] = "CP7M-DEMO-API-001";
        }
        if (string.IsNullOrWhiteSpace(BsonHelpers.GetString(display, "serialNumber")))
        {
            display["serialNumber"] = SampleBatterySerialNumber;
        }
        if (string.IsNullOrWhiteSpace(BsonHelpers.GetString(display, "manufacturerName")))
        {
            display["manufacturerName"] = "Scania Industrial Batteries";
        }

        var media = EnsureDocument(app, "media");
        if (string.IsNullOrWhiteSpace(BsonHelpers.GetString(media, "batteryImageUrl")))
        {
            media["batteryImageUrl"] = BatteryImageCatalog.DefaultImageUrl;
        }

        var operationsDoc = EnsureDocument(app, "operations");
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

        EnsurePublicTrustEnvelope(passport, now);
    }

    private static void EnsurePublicTrustEnvelope(BsonDocument passport, string now)
    {
        var hash = BsonHelpers.GetString(passport, "validation", "hash");
        if (string.IsNullOrWhiteSpace(hash))
        {
            hash = "sample-demo-public-hash";
        }

        var proof = BsonHelpers.GetValue(passport, "trust", "latestProof") as BsonDocument ?? new BsonDocument();
        if (string.IsNullOrWhiteSpace(BsonHelpers.GetString(proof, "proofValue")))
        {
            proof["type"] = "DataIntegrityProof";
            proof["cryptosuite"] = "ecdsa-rdfc-2019";
            proof["created"] = now;
            proof["issuer"] = "Scania Demo";
            proof["verificationMethod"] = "did:web:demo.scania.test#sample-key";
            proof["proofPurpose"] = "assertionMethod";
            proof["proofValue"] = "sample-demo-public-proof";
        }

        var validation = EnsureDocument(passport, "validation");
        validation["isValid"] = true;
        validation["signedAt"] = proof.GetValue("created", now);
        validation["hash"] = hash;
        validation["signature"] = proof.GetValue("proofValue", "sample-demo-public-proof");
        validation["proof"] = proof.DeepClone();

        var validationSummary = new BsonDocument
        {
            ["blockingErrorCount"] = 0,
            ["warningCount"] = 0,
            ["validatedAt"] = now
        };

        var trust = EnsureDocument(passport, "trust");
        trust["state"] = "signed";
        trust["isDirty"] = false;
        trust["latestHash"] = hash;
        trust["latestProof"] = proof;
        trust["lastSignedAt"] = proof.GetValue("created", now);
        trust["validationSummary"] = validationSummary;
    }

    private async Task EnsureSampleTokensAsync(CancellationToken cancellationToken)
    {
        if (_options.EnableDemoData && _options.EnablePublicDemoReadToken)
        {
            await EnsureFixedSampleTokenAsync(
                SampleReadTokenId,
                SampleReadTokenValue,
                "Sample token (read)",
                ExternalTokenAccessMode.Read,
                cancellationToken);
        }

        if (_options.EnableDemoData && _options.EnableDemoWriteSignTesting)
        {
            await EnsureFixedSampleTokenAsync(
                SampleReadWriteTokenId,
                SampleReadWriteTokenValue,
                "Sample token (read-write)",
                ExternalTokenAccessMode.ReadWrite,
                cancellationToken);

            await EnsureFixedSampleTokenAsync(
                SampleSignTokenId,
                SampleSignTokenValue,
                "Sample token (Passport Lifecycle)",
                ExternalTokenAccessMode.Sign,
                cancellationToken);

            await EnsureFixedSampleTokenAsync(
                SampleLifecycleTokenId,
                SampleLifecycleTokenValue,
                "Sample token (Full access)",
                ExternalTokenAccessMode.Lifecycle,
                cancellationToken);
        }
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

            var hasLifecycleToken = existingTokens.Any(token =>
                BsonHelpers.GetString(token, "autoClusterId").Equals(clusterId, StringComparison.OrdinalIgnoreCase)
                && BsonHelpers.GetString(token, "accessMode").Equals("readWriteSign", StringComparison.OrdinalIgnoreCase));
            if (!hasLifecycleToken)
            {
                await _externalApiRepository.CreateTokenAsync(
                    $"{clusterName} - readwritesign",
                    ExternalTokenAccessMode.Lifecycle,
                    [clusterId],
                    allowUnassigned: false,
                    globalAccess: false,
                    actor: "system",
                    autoClusterId: clusterId,
                    cancellationToken: cancellationToken);
            }
        }
    }

    private static BsonDocument BuildFallbackSampleDocument(string sampleBatteryId)
    {
        var now = DateTime.UtcNow.ToString("O");
        var document = new BsonDocument
        {
            ["passportId"] = SamplePassportId,
            ["batteryId"] = sampleBatteryId,
            ["clusterId"] = SampleApiClusterId,
            ["isLatestForBattery"] = true,
            ["snapshot"] = new BsonDocument
            {
                ["batteryFamily"] = SampleBatteryFamily,
                ["batteryModel"] = "1.0",
                ["batterySerialNumber"] = SampleBatterySerialNumber,
                ["createdAt"] = now
            },
            ["registryInfo"] = new BsonDocument
            {
                ["registryId"] = Guid.NewGuid().ToString("N"),
                ["status"] = "published",
                ["createdAt"] = now,
                ["updatedAt"] = now,
                ["hasBeenPublished"] = true
            },
            ["app"] = new BsonDocument
            {
                ["display"] = new BsonDocument
                {
                    ["name"] = "Demo API Compact 7M battery",
                    ["modelNumber"] = "CP7M-DEMO-API-001",
                    ["serialNumber"] = SampleBatterySerialNumber,
                    ["facilityId"] = "DEMO-API-LINE-01",
                    ["manufacturerName"] = "Scania Industrial Batteries"
                },
                ["media"] = new BsonDocument
                {
                    ["batteryImageUrl"] = BatteryImageCatalog.DefaultImageUrl
                },
                ["product"] = new BsonDocument
                {
                    ["productName"] = SampleBatteryFamily,
                    ["productVersion"] = "1.0",
                    ["softwareVersion"] = "2.0",
                    ["softwareReleaseDate"] = now[..10],
                    ["softwareLatestUpdate"] = now[..10]
                },
                ["operations"] = new BsonDocument
                {
                    ["isActive"] = true,
                    ["lastUpdatedAt"] = now,
                    ["locationOfUse"] = new BsonDocument
                    {
                        ["siteName"] = "Sample Site",
                        ["address"] = "Example street 1",
                        ["city"] = "Sample City",
                        ["country"] = "DE"
                    },
                    ["contactPerson"] = new BsonDocument
                    {
                        ["name"] = "Sample Contact",
                        ["email"] = "sample@example.test",
                        ["phone"] = "+49 000 0000"
                    }
                }
            },
            ["aspects"] = new BsonDocument
            {
                ["generalProductInformation"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryCategory"] = BatteryPassCanonicalDataCatalog.DemoBatteryCategory,
                        ["batteryStatus"] = "original",
                        ["batteryMass"] = 420,
                        ["manufacturingDate"] = now[..10],
                        ["productIdentifier"] = "CP7M-DEMO-API-001",
                        ["batteryPassportIdentifier"] = BatteryPassCanonicalDataCatalog.DemoBatteryPassportIdentifier,
                        ["manufacturerInformation"] = new BsonDocument
                        {
                            ["contactName"] = "Scania Industrial Batteries"
                        },
                        ["manufacturingPlace"] = new BsonDocument
                        {
                            ["streetAddress"] = "DEMO-API-LINE-01"
                        }
                    }
                },
                ["materialComposition"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryMaterials"] = new BsonArray(BatteryPassCanonicalDataCatalog.Materials.Take(4).Select(material => new BsonDocument
                        {
                            ["batteryMaterialName"] = material.Label,
                            ["batteryMaterialMass"] = material.DemoMassKg,
                            ["isCriticalRawMaterial"] = material.IsCriticalRawMaterial,
                            ["batteryMaterialLocation"] = new BsonDocument
                            {
                                ["componentName"] = "Battery pack"
                            }
                        }))
                    }
                },
                ["performanceAndDurability"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryTechicalProperties"] = new BsonDocument
                        {
                            ["ratedEnergy"] = 72,
                            ["ratedCapacity"] = 180,
                            ["ratedMaximumPower"] = 120,
                            ["nominalVoltage"] = 400,
                            ["expectedLifetime"] = 8,
                            ["expectedNumberOfCycles"] = 3000
                        }
                    }
                },
                ["carbonFootprintForBatteries"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryCarbonFootprint"] = BatteryPassCanonicalDataCatalog.DemoCarbonFootprint,
                        ["carbonFootprintPerformanceClass"] = BatteryPassCanonicalDataCatalog.DemoPerformanceClass,
                        ["carbonFootprintPerLifecycleStage"] = new BsonArray(BatteryPassCanonicalDataCatalog.CarbonStages.Select(stage => new BsonDocument
                        {
                            ["lifecycleStage"] = stage.Stage,
                            ["carbonFootprint"] = stage.DemoValue
                        }))
                    }
                },
                ["circularity"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["recycledContent"] = new BsonArray
                        {
                            new BsonDocument
                            {
                                ["recycledMaterial"] = "Nickel",
                                ["preConsumerShare"] = 12,
                                ["postConsumerShare"] = 8
                            },
                            new BsonDocument
                            {
                                ["recycledMaterial"] = "Copper",
                                ["preConsumerShare"] = 10,
                                ["postConsumerShare"] = 7
                            }
                        }
                    }
                }
            }
        };

        EnsurePublicTrustEnvelope(document, now);
        return document;
    }

    private static BsonDocument BuildFallbackSampleBattery(string sampleBatteryId)
    {
        var now = DateTime.UtcNow.ToString("O");
        return new BsonDocument
        {
            ["batteryId"] = sampleBatteryId,
            ["clusterId"] = SampleApiClusterId,
            ["createdAt"] = now,
            ["updatedAt"] = now,
            ["identity"] = new BsonDocument
            {
                ["batteryFamily"] = SampleBatteryFamily,
                ["batteryModel"] = "1.0",
                ["serialNumber"] = SampleBatterySerialNumber
            },
            ["app"] = new BsonDocument
            {
                ["display"] = new BsonDocument
                {
                    ["name"] = "Demo API Compact 7M battery",
                    ["modelNumber"] = "CP7M-DEMO-API-001",
                    ["serialNumber"] = SampleBatterySerialNumber,
                    ["facilityId"] = "DEMO-API-LINE-01",
                    ["manufacturerName"] = "Scania Industrial Batteries"
                },
                ["snapshot"] = new BsonDocument
                {
                    ["newPassportRequired"] = false
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
