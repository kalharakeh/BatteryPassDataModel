using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using BCryptNet = BCrypt.Net.BCrypt;

namespace BatteryPassWeb.Services;

public sealed class ProductTemplateService
{
    public static readonly IReadOnlyList<string> FixedPassportIds =
    [
        "did:web:acme.battery.pass:0226151e-949c-d067-8ef3-162431e28976",
        "did:web:acme.battery.pass:sample-customer-north-001",
        "did:web:acme.battery.pass:sample-customer-north-002",
        "did:web:acme.battery.pass:sample-customer-south-001",
        "did:web:acme.battery.pass:sample-customer-south-002",
        "did:web:acme.battery.pass:sample-end-user-fleet-001",
        "did:web:acme.battery.pass:sample-end-user-fleet-002"
    ];

    private static readonly IReadOnlyDictionary<string, (string Name, string UserEmail, string AdminEmail)> FixedClusters =
        new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["cluster-default-demonstrator"] = ("Default Demonstrator Cluster", string.Empty, string.Empty),
            ["demo-cluster"] = ("Demo API Cluster", "demo.user@example.test", "demo.admin@example.test"),
            ["cluster-north-operations"] = ("North Operations Cluster", "north.user@example.test", "north.admin@example.test"),
            ["cluster-south-operations"] = ("South Operations Cluster", "south.user@example.test", "south.admin@example.test"),
            ["cluster-fleet-operations"] = ("Fleet Operations Cluster", "fleet.user@example.test", "fleet.admin@example.test")
        };

    private sealed record SeedBatteryDefinition(
        string ProductId,
        string ClusterId,
        string ModelNumber,
        string SerialNumber,
        string DisplayName,
        string FacilityId,
        IReadOnlyList<SeedPassportSnapshot> Snapshots);

    private sealed record SeedPassportSnapshot(string BatteryModel, int SnapshotOffsetDays);

    private readonly MongoContext _mongoContext;
    private readonly PassportRepository _passportRepository;
    private readonly BatteryRepository _batteryRepository;
    private readonly BatteryPassportSnapshotService _batteryPassportSnapshotService;
    private readonly BatteryIdService _batteryIdService;
    private readonly ClusterRepository _clusterRepository;
    private readonly ExternalApiRepository _externalApiRepository;
    private readonly DataCompletionPolicyService _dataCompletionPolicyService;
    private readonly PassportValidationService _passportValidationService;
    private readonly PassportTrustService _passportTrustService;
    private readonly AuditRevisionService _auditRevisionService;
    private readonly BatteryPassportDeltaService _batteryPassportDeltaService;

    public ProductTemplateService(
        MongoContext mongoContext,
        PassportRepository passportRepository,
        BatteryRepository batteryRepository,
        BatteryPassportSnapshotService batteryPassportSnapshotService,
        BatteryIdService batteryIdService,
        ClusterRepository clusterRepository,
        ExternalApiRepository externalApiRepository,
        DataCompletionPolicyService dataCompletionPolicyService,
        PassportValidationService passportValidationService,
        PassportTrustService passportTrustService,
        AuditRevisionService auditRevisionService,
        BatteryPassportDeltaService batteryPassportDeltaService)
    {
        _mongoContext = mongoContext;
        _passportRepository = passportRepository;
        _batteryRepository = batteryRepository;
        _batteryPassportSnapshotService = batteryPassportSnapshotService;
        _batteryIdService = batteryIdService;
        _clusterRepository = clusterRepository;
        _externalApiRepository = externalApiRepository;
        _dataCompletionPolicyService = dataCompletionPolicyService;
        _passportValidationService = passportValidationService;
        _passportTrustService = passportTrustService;
        _auditRevisionService = auditRevisionService;
        _batteryPassportDeltaService = batteryPassportDeltaService;
    }

    public async Task<IReadOnlyList<BatteryProductTemplate>> ListProductsAsync(CancellationToken cancellationToken = default)
    {
        var productCollection = ProductCollection();
        var productVersionCollection = ProductVersionCollection();
        if (productCollection == null || productVersionCollection == null)
        {
            return BatteryProductTemplateCatalog.DefaultProducts;
        }

        await EnsureDefaultTemplatesAsync("system", cancellationToken);
        var products = await productCollection
            .Find(Builders<BsonDocument>.Filter.Empty)
            .SortBy(product => product["productName"])
            .ToListAsync(cancellationToken);
        var productVersions = await productVersionCollection
            .Find(Builders<BsonDocument>.Filter.Empty)
            .ToListAsync(cancellationToken);

        return products
            .Select(product =>
            {
                var productId = BsonHelpers.GetString(product, "productId");
                var template = ProductTemplatePassportBuilder.FromProductDocument(product);
                var versions = productVersions
                    .Where(item => BsonHelpers.GetString(item, "productId").Equals(productId, StringComparison.OrdinalIgnoreCase))
                    .Select(version => ProductTemplatePassportBuilder.FromProductVersionDocument(version, template))
                    .OrderBy(version => version.Version, VersionStringComparer.Descending)
                    .ToList();

                return template with
                {
                    ProductVersions = versions.Count == 0 ? template.ProductVersions : versions
                };
            })
            .ToList();
    }

    public async Task<BatteryProductTemplate?> GetProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            return null;
        }

        var products = await ListProductsAsync(cancellationToken);
        return products.FirstOrDefault(product => product.ProductId.Equals(productId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<BsonDocument?> GetProductDocumentAsync(string productId, CancellationToken cancellationToken = default)
    {
        var collection = ProductCollection();
        if (collection == null || string.IsNullOrWhiteSpace(productId))
        {
            return null;
        }

        await EnsureDefaultTemplatesAsync("system", cancellationToken);
        return await collection.Find(Builders<BsonDocument>.Filter.Eq("productId", productId.Trim())).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task EnsureDefaultTemplatesAsync(string actor, CancellationToken cancellationToken = default, bool force = false)
    {
        var productCollection = ProductCollection();
        var productVersionCollection = ProductVersionCollection();
        if (productCollection == null || productVersionCollection == null)
        {
            return;
        }

        await DropLegacyVersionParameterStoreAsync(cancellationToken);

        if (!force && await productCollection.CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty, cancellationToken: cancellationToken) > 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToString("O");
        foreach (var product in BatteryProductTemplateCatalog.DefaultProducts)
        {
            var productDocument = ProductTemplatePassportBuilder.ToProductDocument(product, now, actor);
            productDocument["templateDocumentReferences"] = new BsonArray(await BuildTemplateDocumentReferencesAsync(product, cancellationToken));
            await productCollection.ReplaceOneAsync(
                Builders<BsonDocument>.Filter.Eq("productId", product.ProductId),
                productDocument,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);

            await productVersionCollection.DeleteManyAsync(
                Builders<BsonDocument>.Filter.Eq("productId", product.ProductId),
                cancellationToken);

            foreach (var productVersion in product.ProductVersions)
            {
                var productVersionDocument = ProductTemplatePassportBuilder.ToProductVersionDocument(product.ProductId, productVersion, now, actor);
                productVersionDocument["templateDocumentReferences"] = productDocument["templateDocumentReferences"].DeepClone();
                await productVersionCollection.ReplaceOneAsync(
                    Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Eq("productId", product.ProductId),
                        Builders<BsonDocument>.Filter.Eq("version", productVersion.Version)),
                    productVersionDocument,
                    new ReplaceOptions { IsUpsert = true },
                    cancellationToken);

                await _dataCompletionPolicyService.SaveProductVersionPolicyAsync(
                    product.ProductId,
                    productVersion.Version,
                    productVersion.RequiredFieldKeys,
                    actor,
                    cancellationToken);
            }

            await _dataCompletionPolicyService.SaveProductPolicyAsync(product.ProductId, product.RequiredFieldKeys, actor, cancellationToken);
        }
    }

    public async Task<BsonDocument> BuildPassportFromTemplateAsync(
        string passportId,
        string productId,
        string softwareVersion,
        ProductTemplateBatteryIdentity identity,
        string actor,
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultTemplatesAsync(actor, cancellationToken);
        var product = await GetProductAsync(productId, cancellationToken) ?? BatteryProductTemplateCatalog.DefaultProduct;
        var selectedProductVersion = product.ProductVersions.FirstOrDefault(version =>
                version.SoftwareVersion.Equals(softwareVersion, StringComparison.OrdinalIgnoreCase))
            ?? product.LatestProductVersion;
        return await BuildPassportFromTemplateAsync(
            passportId,
            product.ProductId,
            selectedProductVersion.Version,
            softwareVersion,
            identity,
            actor,
            cancellationToken);
    }

    public async Task<BsonDocument> BuildPassportFromTemplateAsync(
        string passportId,
        string productId,
        string productVersion,
        string softwareVersion,
        ProductTemplateBatteryIdentity identity,
        string actor,
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultTemplatesAsync(actor, cancellationToken);
        var product = await GetProductAsync(productId, cancellationToken) ?? BatteryProductTemplateCatalog.DefaultProduct;
        var selectedProductVersion = product.ProductVersions.FirstOrDefault(version => version.Version.Equals(productVersion, StringComparison.OrdinalIgnoreCase))
            ?? product.LatestProductVersion;
        var now = DateTimeOffset.UtcNow.ToString("O");
        var document = ProductTemplatePassportBuilder.BuildPassportFromTemplate(passportId, product, selectedProductVersion, identity, now);
        var productDocument = await GetProductDocumentAsync(product.ProductId, cancellationToken);
        ApplyTemplateDocumentReferences(document, productDocument);
        document["app"]["templateBaseline"] = ProductTemplatePassportBuilder.BuildTemplateBaseline(document);
        return document;
    }

    public async Task SaveProductAsync(
        BatteryProductTemplate product,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var productCollection = ProductCollection();
        var productVersionCollection = ProductVersionCollection();
        if (productCollection == null || productVersionCollection == null)
        {
            return;
        }

        await DropLegacyVersionParameterStoreAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow.ToString("O");
        var existingProductDocument = await productCollection
            .Find(Builders<BsonDocument>.Filter.Eq("productId", product.ProductId))
            .FirstOrDefaultAsync(cancellationToken);
        var productDocument = ProductTemplatePassportBuilder.ToProductDocument(product, now, actor);
        var existingReferences = existingProductDocument?.GetValue("templateDocumentReferences", BsonNull.Value) as BsonArray;
        productDocument["templateDocumentReferences"] = existingReferences is { Count: > 0 }
            ? existingReferences.DeepClone()
            : new BsonArray(await BuildTemplateDocumentReferencesAsync(product, cancellationToken));

        await productCollection.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("productId", product.ProductId),
            productDocument,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        await productVersionCollection.DeleteManyAsync(
            Builders<BsonDocument>.Filter.Eq("productId", product.ProductId),
            cancellationToken);

        var productVersions = product.ProductVersions.Count == 0
            ? [product.LatestProductVersion]
            : product.ProductVersions;
        foreach (var productVersion in productVersions)
        {
            var productVersionDocument = ProductTemplatePassportBuilder.ToProductVersionDocument(product.ProductId, productVersion, now, actor);
            productVersionDocument["templateDocumentReferences"] = productDocument["templateDocumentReferences"].DeepClone();
            await productVersionCollection.ReplaceOneAsync(
                Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("productId", product.ProductId),
                    Builders<BsonDocument>.Filter.Eq("version", productVersion.Version)),
                productVersionDocument,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);

            await _dataCompletionPolicyService.SaveProductVersionPolicyAsync(
                product.ProductId,
                productVersion.Version,
                productVersion.RequiredFieldKeys,
                actor,
                cancellationToken);
        }

        await _dataCompletionPolicyService.SaveProductPolicyAsync(product.ProductId, product.RequiredFieldKeys, actor, cancellationToken);
    }

    public async Task ApplyProductTemplateReferencesAsync(
        BsonDocument passport,
        string productId,
        CancellationToken cancellationToken = default)
    {
        var productDocument = await GetProductDocumentAsync(productId, cancellationToken);
        ApplyTemplateDocumentReferences(passport, productDocument);
    }

    public async Task<ProductTemplateResetResult> ResetTemplateDemoAsync(string actor, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultTemplatesAsync(actor, cancellationToken, force: true);
        await EnsureFixedClustersAndUsersAsync(cancellationToken);
        await EnsureFixedApiDemoTokensAsync(cancellationToken);

        var passportCollection = PassportCollection();
        var batteryCollection = BatteryCollection();
        if (passportCollection == null || batteryCollection == null)
        {
            return new ProductTemplateResetResult(0, [], DateTimeOffset.UtcNow.ToString("O"));
        }

        await _batteryRepository.EnsureIndexesAsync(cancellationToken);

        var existingIds = await passportCollection
            .Find(Builders<BsonDocument>.Filter.Empty)
            .Project(Builders<BsonDocument>.Projection.Include("passportId").Exclude("_id"))
            .ToListAsync(cancellationToken);
        var allExistingIds = existingIds
            .Select(document => BsonHelpers.GetString(document, "passportId"))
            .Where(passportId => !string.IsNullOrWhiteSpace(passportId))
            .ToList();
        await _auditRevisionService.DeleteDemoLedgerAsync(allExistingIds.Concat(FixedPassportIds).ToArray(), cancellationToken);
        await passportCollection.DeleteManyAsync(Builders<BsonDocument>.Filter.Empty, cancellationToken);
        await batteryCollection.DeleteManyAsync(Builders<BsonDocument>.Filter.Empty, cancellationToken);
        await _mongoContext.Database!.GetCollection<BsonDocument>("batteryTelemetry").DeleteManyAsync(Builders<BsonDocument>.Filter.Empty, cancellationToken);

        var resetInstant = DateTimeOffset.UtcNow;
        var resetAt = resetInstant.ToString("O");

        // Seeded batteries: the API demo seed intentionally creates multiple passports
        // so historical/latest behavior is visible immediately after a reset.
        var batterySeeds = new SeedBatteryDefinition[]
        {
            new("compact-7m", string.Empty, "CP7M-DEMO-001", "SN-0226151E", "Compact 7M unassigned demonstrator battery", "DEFAULT-LINE-01", [new("1.0", -45)]),
            new("compact-7m", "demo-cluster", "CP7M-DEMO-API-001", ExternalApiInitializer.SampleBatterySerialNumber, "Demo API Compact 7M battery", "DEMO-API-LINE-01", [new("1.0", -30), new("2.0", 0)]),
            new("compact-7m", "cluster-north-operations", "CP7M-NORTH-001", "SN-NORTH-001", "North Compact 7M customer battery", "NORTH-LINE-01", [new("2.0", 0)]),
            new("compact-7m", "cluster-north-operations", "CP7M-NORTH-002", "SN-NORTH-002", "North Compact 7M customer battery 2", "NORTH-LINE-02", [new("2.0", -4)]),
            new("compact-13m", "cluster-south-operations", "CP13M-SOUTH-001", "SN-SOUTH-001", "South Compact 13M customer battery", "SOUTH-LINE-01", [new("1.0", -18)]),
            new("compact-13m", "cluster-south-operations", "CP13M-SOUTH-002", "SN-SOUTH-002", "South Compact 13M customer battery 2", "SOUTH-LINE-02", [new("2.0", -2)]),
            new("core", "cluster-fleet-operations", "CORE-FLEET-001", "SN-FLEET-001", "Fleet Core customer battery", "FLEET-LINE-01", [new("1.0", -12)]),
            new("core", "cluster-fleet-operations", "CORE-FLEET-002", "SN-FLEET-002", "Fleet Core customer battery 2", "FLEET-LINE-02", [new("2.0", -1)])
        };

        var products = await ListProductsAsync(cancellationToken);
        var productsById = products.ToDictionary(product => product.ProductId, StringComparer.OrdinalIgnoreCase);
        var batteryIds = new List<string>();
        var passportIds = new List<string>();

        foreach (var seed in batterySeeds)
        {
            var product = productsById.TryGetValue(seed.ProductId, out var selectedProduct)
                ? selectedProduct
                : BatteryProductTemplateCatalog.DefaultProduct;
            var productDocument = await GetProductDocumentAsync(product.ProductId, cancellationToken);
            var batteryId = _batteryIdService.CreateBatteryId(product.ProductName, seed.SerialNumber);
            batteryIds.Add(batteryId);

            foreach (var snapshotSeed in seed.Snapshots.OrderBy(snapshot => snapshot.SnapshotOffsetDays))
            {
                var snapshotInstant = resetInstant.AddDays(snapshotSeed.SnapshotOffsetDays);
                var snapshotAt = snapshotInstant.ToString("O");
                var productVersion = product.ProductVersions.FirstOrDefault(version => version.Version.Equals(snapshotSeed.BatteryModel, StringComparison.OrdinalIgnoreCase))
                    ?? product.LatestProductVersion;
                var battery = ProductTemplatePassportBuilder.BuildBatteryFromTemplate(
                    batteryId,
                    product,
                    productVersion,
                    new ProductTemplateBatteryIdentity
                    {
                        ClusterId = seed.ClusterId,
                        ModelNumber = seed.ModelNumber,
                        SerialNumber = seed.SerialNumber,
                        DisplayName = seed.DisplayName,
                        FacilityId = seed.FacilityId,
                        ManufacturingDate = snapshotAt[..10]
                    },
                    snapshotAt);
                ApplyTemplateDocumentReferences(battery, productDocument);
                battery["app"]["templateBaseline"] = ProductTemplatePassportBuilder.BuildTemplateBaseline(battery);
                await _batteryRepository.ReplaceAsync(batteryId, battery, cancellationToken);

                var passport = await _batteryPassportSnapshotService.CreatePassportSnapshotAsync(battery, actor, snapshotInstant, cancellationToken);
                ApplyTemplateDocumentReferences(passport, productDocument);
                passport["app"]["templateBaseline"] = ProductTemplatePassportBuilder.BuildTemplateBaseline(passport);
                passport.Remove("_id");
                var passportId = BsonHelpers.GetString(passport, "passportId");
                await _passportRepository.ReplaceAsync(passportId, passport, cancellationToken);
                await SignAndPublishSeedPassportAsync(passport, actor, resetAt, seed.Snapshots.Count > 1, cancellationToken);
                passportIds.Add(passportId);
            }
        }

        return new ProductTemplateResetResult(passportIds.Count, passportIds, resetAt)
        {
            BatteryCount = batteryIds.Count,
            BatteryIds = batteryIds
        };
    }

    private async Task SignAndPublishSeedPassportAsync(
        BsonDocument passport,
        string actor,
        string resetAt,
        bool multiplePassports,
        CancellationToken cancellationToken)
    {
        var passportId = BsonHelpers.GetString(passport, "passportId");
        var policy = await _dataCompletionPolicyService.GetPolicyForPassportAsync(passport, cancellationToken);
        var summary = _passportValidationService.Validate(passport, policy);
        var signature = _passportTrustService.Sign(passport, actor);
        var revision = await _auditRevisionService.CreateSignedRevisionAsync(
            passportId,
            signature.Snapshot,
            signature.Hash,
            signature.Proof,
            actor,
            signature.SignedAt,
            cancellationToken);
        var revisionId = BsonHelpers.GetString(revision, "revisionId");
        await _passportRepository.UpdateTrustSignatureAsync(passportId, summary, signature.Hash, signature.Proof, revisionId, signature.SignedAt, cancellationToken);
        await _passportRepository.PublishPassportAsync(passportId, revisionId, resetAt, $"sha256:{signature.Hash}", signature.Proof, cancellationToken);
        await _auditRevisionService.MarkRevisionPublishedAsync(revisionId, resetAt, cancellationToken);
        await _auditRevisionService.AppendAuditEventAsync(
            passportId,
            "passport.productTemplate.seeded",
            actor,
            "admin",
            "product-template-reset",
            "Seeded batteries and passport snapshots from product template reset.",
            new BsonDocument
            {
                ["batteryId"] = BsonHelpers.GetString(passport, "batteryId"),
                ["batteryFamily"] = BsonHelpers.GetString(passport, "snapshot", "batteryFamily"),
                ["batteryModel"] = BsonHelpers.GetString(passport, "snapshot", "batteryModel"),
                ["multiplePassports"] = multiplePassports,
                ["resetAt"] = resetAt
            },
            cancellationToken);
    }

    public async Task<ProductTemplatePushResult> PushProductVersionAsync(
        string productId,
        string productVersion,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var product = await GetProductAsync(productId, cancellationToken);
        if (product == null)
        {
            return new ProductTemplatePushResult();
        }

        var selectedProductVersion = product.ProductVersions.FirstOrDefault(version => version.Version.Equals(productVersion, StringComparison.OrdinalIgnoreCase))
            ?? product.LatestProductVersion;
        var collection = BatteryCollection();
        if (collection == null)
        {
            return new ProductTemplatePushResult();
        }

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("app.product.productId", product.ProductId),
            Builders<BsonDocument>.Filter.Eq("app.product.productVersion", selectedProductVersion.Version));
        var batteries = await collection.Find(filter).ToListAsync(cancellationToken);
        var changedBatteryIds = new List<string>();
        var skippedPaths = new List<string>();
        var now = DateTimeOffset.UtcNow.ToString("O");

        foreach (var battery in batteries)
        {
            var before = battery.DeepClone().AsBsonDocument;
            var oldTemplate = BsonHelpers.GetValue(before, "app", "templateBaseline") as BsonDocument
                ?? ProductTemplatePassportBuilder.BuildTemplateBaseline(before);
            ProductTemplatePassportBuilder.ApplyProductVersionToBattery(battery, product, selectedProductVersion, now);
            var newTemplate = ProductTemplatePassportBuilder.BuildTemplateBaseline(battery);
            var result = ProductTemplatePassportBuilder.ComputeSafeTemplateUpdates(before, oldTemplate, newTemplate);
            if (result.UpdatedPaths.Count == 0)
            {
                skippedPaths.AddRange(result.SkippedOverridePaths);
                continue;
            }

            var batteryId = BsonHelpers.GetString(result.UpdatedPassport, "batteryId");
            result.UpdatedPassport["updatedAt"] = now;
            await _batteryRepository.ReplaceAsync(batteryId, result.UpdatedPassport, cancellationToken);
            await _batteryPassportDeltaService.UpdateNewPassportRequiredAsync(result.UpdatedPassport, cancellationToken);
            changedBatteryIds.Add(batteryId);
            skippedPaths.AddRange(result.SkippedOverridePaths);
        }

        await RecordPushRunAsync(product.ProductId, selectedProductVersion.Version, string.Empty, actor, now, batteries.Count, changedBatteryIds.Count, skippedPaths, cancellationToken);

        return new ProductTemplatePushResult
        {
            MatchedBatteries = batteries.Count,
            UpdatedBatteries = changedBatteryIds.Count,
            SkippedBatteries = batteries.Count - changedBatteryIds.Count,
            UpdatedBatteryIds = changedBatteryIds,
            UpdatedPassportIds = changedBatteryIds,
            SkippedOverridePaths = skippedPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    public async Task<ProductTemplateSafeUpdateResult?> ChangePassportBatteryVersionAsync(
        BsonDocument passport,
        string requestedBatteryVersion,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var productId = BsonHelpers.GetString(passport, "app", "product", "productId");
        if (string.IsNullOrWhiteSpace(productId) || string.IsNullOrWhiteSpace(requestedBatteryVersion))
        {
            return null;
        }

        var product = await GetProductAsync(productId, cancellationToken);
        if (product == null)
        {
            return null;
        }

        var selectedVersion = product.ProductVersions.FirstOrDefault(version =>
            version.Version.Equals(requestedBatteryVersion.Trim(), StringComparison.OrdinalIgnoreCase));
        if (selectedVersion == null)
        {
            return null;
        }

        var productDocument = await GetProductDocumentAsync(product.ProductId, cancellationToken);
        return BuildSafeTemplatePushUpdate(
            passport,
            product,
            selectedVersion,
            productDocument,
            DateTime.UtcNow.ToString("O"));
    }

    private static ProductTemplateSafeUpdateResult BuildSafeTemplatePushUpdate(
        BsonDocument passport,
        BatteryProductTemplate product,
        BatteryProductVersion selectedProductVersion,
        BsonDocument? productDocument,
        string now)
    {
        var oldTemplate = BsonHelpers.GetValue(passport, "app", "templateBaseline") as BsonDocument ?? new BsonDocument();
        var identity = new ProductTemplateBatteryIdentity
        {
            ClusterId = BsonHelpers.GetString(passport, "clusterId"),
            ModelNumber = BsonHelpers.GetString(passport, "app", "display", "modelNumber"),
            SerialNumber = BsonHelpers.GetString(passport, "app", "display", "serialNumber"),
            DisplayName = BsonHelpers.GetString(passport, "app", "display", "name"),
            FacilityId = BsonHelpers.GetString(passport, "app", "display", "facilityId"),
            ManufacturingDate = BsonHelpers.GetString(passport, "aspects", "generalProductInformation", "payload", "manufacturingDate")
        };
        var fresh = ProductTemplatePassportBuilder.BuildPassportFromTemplate(
            BsonHelpers.GetString(passport, "passportId"),
            product,
            selectedProductVersion,
            identity,
            now);
        ApplyTemplateDocumentReferences(fresh, productDocument);
        var newTemplate = ProductTemplatePassportBuilder.BuildTemplateBaseline(fresh);
        return ProductTemplatePassportBuilder.ComputeSafeTemplateUpdates(passport, oldTemplate, newTemplate);
    }

    private async Task RecordPushRunAsync(
        string productId,
        string productVersion,
        string softwareVersion,
        string actor,
        string now,
        int matchedBatteries,
        int updatedBatteries,
        IReadOnlyList<string> skippedPaths,
        CancellationToken cancellationToken)
    {
        var pushRunsCollection = PushRunsCollection();
        if (pushRunsCollection == null)
        {
            return;
        }

        await pushRunsCollection.InsertOneAsync(new BsonDocument
        {
            ["productId"] = productId,
            ["productVersion"] = productVersion,
            ["softwareVersion"] = softwareVersion,
            ["actor"] = actor,
            ["createdAt"] = now,
            ["matchedBatteries"] = matchedBatteries,
            ["updatedBatteries"] = updatedBatteries,
            ["skippedOverridePaths"] = new BsonArray(skippedPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        }, cancellationToken: cancellationToken);
    }

    private async Task EnsureFixedClustersAndUsersAsync(CancellationToken cancellationToken)
    {
        const string password = "Password123!";
        var passwordHash = BCryptNet.HashPassword(password);
        const string northCustomerEmail = "customer_001_001@customer.org";
        const string northCustomerPassword = "12345";
        var northCustomerPasswordHash = BCryptNet.HashPassword(northCustomerPassword);
        await _clusterRepository.UpsertUserAsync("admin@example.test", "General Admin", ["admin"], passwordHash, cancellationToken);
        foreach (var (clusterId, definition) in FixedClusters)
        {
            await _clusterRepository.UpsertClusterAsync(clusterId, definition.Name, cancellationToken);
            if (!string.IsNullOrWhiteSpace(definition.UserEmail))
            {
                await _clusterRepository.UpsertUserAsync(definition.UserEmail, definition.UserEmail.Split('@')[0], ["viewer"], passwordHash, cancellationToken);
                await _clusterRepository.UpsertClusterMembershipAsync(definition.UserEmail, clusterId, "member", cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(definition.AdminEmail))
            {
                await _clusterRepository.UpsertUserAsync(definition.AdminEmail, definition.AdminEmail.Split('@')[0], ["viewer"], passwordHash, cancellationToken);
                await _clusterRepository.UpsertClusterMembershipAsync(definition.AdminEmail, clusterId, "clusterAdmin", cancellationToken);
            }
        }

        await _clusterRepository.UpsertUserAsync(northCustomerEmail, "North Customer 001", ["viewer"], northCustomerPasswordHash, cancellationToken);
        await _clusterRepository.UpsertClusterMembershipAsync(northCustomerEmail, "cluster-north-operations", "member", cancellationToken);
    }

    private async Task EnsureFixedApiDemoTokensAsync(CancellationToken cancellationToken)
    {
        if (!_externalApiRepository.IsAvailable)
        {
            return;
        }

        await _externalApiRepository.UpsertFixedTokenAsync(
            ExternalApiInitializer.SampleReadTokenId,
            ExternalApiInitializer.SampleReadTokenValue,
            "Sample token (read)",
            ExternalTokenAccessMode.Read,
            [ExternalApiInitializer.SampleApiClusterId],
            allowUnassigned: false,
            globalAccess: false,
            actor: "system",
            isSample: true,
            cancellationToken);

        await _externalApiRepository.UpsertFixedTokenAsync(
            ExternalApiInitializer.SampleReadWriteTokenId,
            ExternalApiInitializer.SampleReadWriteTokenValue,
            "Sample token (read-write)",
            ExternalTokenAccessMode.ReadWrite,
            [ExternalApiInitializer.SampleApiClusterId],
            allowUnassigned: false,
            globalAccess: false,
            actor: "system",
            isSample: true,
            cancellationToken);

        await _externalApiRepository.UpsertFixedTokenAsync(
            ExternalApiInitializer.SampleSignTokenId,
            ExternalApiInitializer.SampleSignTokenValue,
            "Sample token (validate, sign, publish)",
            ExternalTokenAccessMode.Sign,
            [ExternalApiInitializer.SampleApiClusterId],
            allowUnassigned: false,
            globalAccess: false,
            actor: "system",
            isSample: true,
            cancellationToken);
    }

    private async Task<IReadOnlyList<BsonDocument>> BuildTemplateDocumentReferencesAsync(
        BatteryProductTemplate product,
        CancellationToken cancellationToken)
    {
        var references = new List<BsonDocument>();
        foreach (var document in product.TemplateDocuments)
        {
            var reference = await EnsureProductEvidenceFileAsync(product.ProductId, document, cancellationToken);
            references.Add(reference);
        }

        return references;
    }

    private async Task<BsonDocument> EnsureProductEvidenceFileAsync(
        string productId,
        ProductTemplateDocumentSeed document,
        CancellationToken cancellationToken)
    {
        if (_mongoContext.Database == null)
        {
            return FallbackDocumentReference(productId, document);
        }

        var files = _mongoContext.Database.GetCollection<BsonDocument>("passportFiles.files");
        var existing = await files.Find(Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("metadata.productId", productId),
                Builders<BsonDocument>.Filter.Eq("metadata.documentKey", document.DocumentKey)))
            .FirstOrDefaultAsync(cancellationToken);
        if (existing != null)
        {
            var existingId = BsonHelpers.GetString(existing, "_id");
            var metadata = existing.GetValue("metadata", new BsonDocument()) as BsonDocument ?? new BsonDocument();
            return new BsonDocument
            {
                ["documentKey"] = document.DocumentKey,
                ["label"] = document.Label,
                ["fileId"] = existingId,
                ["url"] = $"/api/files/{existingId}",
                ["contentType"] = BsonHelpers.GetString(metadata, "contentType"),
                ["sha256"] = BsonHelpers.GetString(metadata, "sha256"),
                ["visibility"] = document.Visibility,
                ["source"] = "productTemplate"
            };
        }

        var path = Path.Combine(ResolveRepoRoot(), "artifacts", "evidence-upload-samples", document.FileName);
        if (!File.Exists(path))
        {
            return FallbackDocumentReference(productId, document);
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        var bucket = new GridFSBucket(_mongoContext.Database, new GridFSBucketOptions { BucketName = "passportFiles" });
        var uploadMetadata = new BsonDocument
        {
            ["uploadedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["source"] = "product-template-seed",
            ["productId"] = productId,
            ["documentKey"] = document.DocumentKey,
            ["contentType"] = "application/pdf",
            ["sha256"] = sha256,
            ["visibility"] = document.Visibility
        };
        var fileId = await bucket.UploadFromBytesAsync(document.FileName, bytes, new GridFSUploadOptions { Metadata = uploadMetadata }, cancellationToken);
        return new BsonDocument
        {
            ["documentKey"] = document.DocumentKey,
            ["label"] = document.Label,
            ["fileId"] = fileId.ToString(),
            ["url"] = $"/api/files/{fileId}",
            ["contentType"] = "application/pdf",
            ["sha256"] = sha256,
            ["visibility"] = document.Visibility,
            ["source"] = "productTemplate"
        };
    }

    private static BsonDocument FallbackDocumentReference(string productId, ProductTemplateDocumentSeed document)
    {
        return new BsonDocument
        {
            ["documentKey"] = document.DocumentKey,
            ["label"] = document.Label,
            ["fileId"] = string.Empty,
            ["url"] = $"template://{productId}/{document.DocumentKey}/{document.FileName}",
            ["contentType"] = "application/pdf",
            ["sha256"] = $"template-{productId}-{document.DocumentKey}-sha256",
            ["visibility"] = document.Visibility,
            ["source"] = "productTemplate"
        };
    }

    private static void ApplyTemplateDocumentReferences(BsonDocument passport, BsonDocument? productDocument)
    {
        if (productDocument == null || productDocument.GetValue("templateDocumentReferences", new BsonArray()) is not BsonArray references)
        {
            return;
        }

        var documentOwnerId = BsonHelpers.GetString(passport, "passportId");
        if (string.IsNullOrWhiteSpace(documentOwnerId))
        {
            documentOwnerId = BsonHelpers.GetString(passport, "batteryId");
        }

        var escapedDocumentOwnerId = Uri.EscapeDataString(documentOwnerId);
        var documents = EnsureDocument(EnsureDocument(passport, "app"), "documents");
        foreach (var reference in references.OfType<BsonDocument>())
        {
            var key = BsonHelpers.GetString(reference, "documentKey");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var fileId = BsonHelpers.GetString(reference, "fileId");
            var url = string.IsNullOrWhiteSpace(fileId)
                ? BsonHelpers.GetString(reference, "url")
                : $"/api/files/{fileId}?passportId={escapedDocumentOwnerId}";
            documents[key] = new BsonDocument
            {
                ["label"] = BsonHelpers.GetString(reference, "label"),
                ["fileId"] = fileId,
                ["url"] = url,
                ["contentType"] = BsonHelpers.GetString(reference, "contentType"),
                ["sha256"] = BsonHelpers.GetString(reference, "sha256"),
                ["visibility"] = BsonHelpers.GetString(reference, "visibility"),
                ["source"] = "productTemplate"
            };
        }

        var aspects = EnsureDocument(passport, "aspects");
        var labelingPayload = EnsureDocument(EnsureDocument(aspects, "labeling"), "payload");
        labelingPayload["resultOfTestReport"] = BsonHelpers.GetString(documents, "conformityAssessment", "url");
        labelingPayload["declarationOfConformity"] = BsonHelpers.GetString(documents, "euDeclarationOfConformity", "url");
        var supplyPayload = EnsureDocument(EnsureDocument(aspects, "supplyChainDueDiligence"), "payload");
        supplyPayload["sustainabilityReport"] = BsonHelpers.GetString(documents, "sustainabilityReport", "url");
        supplyPayload["supplyChainDueDiligenceReport"] = BsonHelpers.GetString(documents, "dueDiligenceReport", "url");
        supplyPayload["thirdPartyAussurances"] = BsonHelpers.GetString(documents, "thirdPartyAudit", "url");
        supplyPayload["taxonomyReport"] = BsonHelpers.GetString(documents, "taxonomyReport", "url");
        var carbonPayload = EnsureDocument(EnsureDocument(aspects, "carbonFootprintForBatteries"), "payload");
        carbonPayload["carbonFootprintStudy"] = BsonHelpers.GetString(documents, "co2StudyReference", "url");
    }

    private IMongoCollection<BsonDocument>? ProductCollection() =>
        _mongoContext.Database?.GetCollection<BsonDocument>("batteryProductTemplates");

    private IMongoCollection<BsonDocument>? ProductVersionCollection() =>
        _mongoContext.Database?.GetCollection<BsonDocument>("batteryProductTemplateVersions");

    private IMongoCollection<BsonDocument>? PassportCollection() =>
        _mongoContext.Database?.GetCollection<BsonDocument>("passports");

    private IMongoCollection<BsonDocument>? BatteryCollection() =>
        _mongoContext.Database?.GetCollection<BsonDocument>("batteries");

    private IMongoCollection<BsonDocument>? PushRunsCollection() =>
        _mongoContext.Database?.GetCollection<BsonDocument>("batteryProductTemplatePushRuns");

    private async Task DropLegacyVersionParameterStoreAsync(CancellationToken cancellationToken)
    {
        if (_mongoContext.Database == null)
        {
            return;
        }

        var legacyName = "batteryProductTemplate" + "SoftwareVersions";
        var collections = await _mongoContext.Database.ListCollectionNames().ToListAsync(cancellationToken);
        if (collections.Contains(legacyName, StringComparer.OrdinalIgnoreCase))
        {
            await _mongoContext.Database.DropCollectionAsync(legacyName, cancellationToken);
        }
    }

    private sealed class VersionStringComparer : IComparer<string>
    {
        public static readonly VersionStringComparer Ascending = new(descending: false);
        public static readonly VersionStringComparer Descending = new(descending: true);

        private readonly bool _descending;

        private VersionStringComparer(bool descending)
        {
            _descending = descending;
        }

        public int Compare(string? x, string? y)
        {
            var result = CompareAscending(x ?? string.Empty, y ?? string.Empty);
            return _descending ? -result : result;
        }

        private static int CompareAscending(string left, string right)
        {
            var leftParts = left.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var rightParts = right.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var max = Math.Max(leftParts.Length, rightParts.Length);
            for (var index = 0; index < max; index++)
            {
                var leftValue = index < leftParts.Length && int.TryParse(leftParts[index], out var parsedLeft) ? parsedLeft : 0;
                var rightValue = index < rightParts.Length && int.TryParse(rightParts[index], out var parsedRight) ? parsedRight : 0;
                var partResult = leftValue.CompareTo(rightValue);
                if (partResult != 0)
                {
                    return partResult;
                }
            }

            return StringComparer.OrdinalIgnoreCase.Compare(left, right);
        }
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

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "README.md"))
                && Directory.Exists(Path.Combine(directory.FullName, "artifacts")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
