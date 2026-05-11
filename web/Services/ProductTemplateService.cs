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
        "did:web:acme.battery.pass:sample-customer-south-001",
        "did:web:acme.battery.pass:sample-end-user-fleet-001"
    ];

    private static readonly IReadOnlyDictionary<string, (string Name, string UserEmail, string AdminEmail)> FixedClusters =
        new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["cluster-default-demonstrator"] = ("Default Demonstrator Cluster", string.Empty, string.Empty),
            ["cluster-north-operations"] = ("North Operations Cluster", "north.user@example.test", "north.admin@example.test"),
            ["cluster-south-operations"] = ("South Operations Cluster", "south.user@example.test", "south.admin@example.test"),
            ["cluster-fleet-operations"] = ("Fleet Operations Cluster", "fleet.user@example.test", "fleet.admin@example.test")
        };

    private readonly MongoContext _mongoContext;
    private readonly PassportRepository _passportRepository;
    private readonly ClusterRepository _clusterRepository;
    private readonly DataCompletionPolicyService _dataCompletionPolicyService;
    private readonly PassportValidationService _passportValidationService;
    private readonly PassportTrustService _passportTrustService;
    private readonly AuditRevisionService _auditRevisionService;

    public ProductTemplateService(
        MongoContext mongoContext,
        PassportRepository passportRepository,
        ClusterRepository clusterRepository,
        DataCompletionPolicyService dataCompletionPolicyService,
        PassportValidationService passportValidationService,
        PassportTrustService passportTrustService,
        AuditRevisionService auditRevisionService)
    {
        _mongoContext = mongoContext;
        _passportRepository = passportRepository;
        _clusterRepository = clusterRepository;
        _dataCompletionPolicyService = dataCompletionPolicyService;
        _passportValidationService = passportValidationService;
        _passportTrustService = passportTrustService;
        _auditRevisionService = auditRevisionService;
    }

    public async Task<IReadOnlyList<BatteryProductTemplate>> ListProductsAsync(CancellationToken cancellationToken = default)
    {
        var productCollection = ProductCollection();
        var productVersionCollection = ProductVersionCollection();
        var softwareCollection = SoftwareCollection();
        if (productCollection == null || productVersionCollection == null || softwareCollection == null)
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
        var software = await softwareCollection
            .Find(Builders<BsonDocument>.Filter.Empty)
            .ToListAsync(cancellationToken);

        return products
            .Select(product =>
            {
                var productId = BsonHelpers.GetString(product, "productId");
                var legacySoftware = software
                    .Where(item => BsonHelpers.GetString(item, "productId").Equals(productId, StringComparison.OrdinalIgnoreCase)
                                   && string.IsNullOrWhiteSpace(BsonHelpers.GetString(item, "productVersion")))
                    .OrderBy(item => BsonHelpers.GetString(item, "version"), VersionStringComparer.Descending)
                    .Select(ProductTemplatePassportBuilder.FromSoftwareDocument)
                    .ToList();
                var template = ProductTemplatePassportBuilder.FromProductDocument(product, legacySoftware);
                var versions = productVersions
                    .Where(item => BsonHelpers.GetString(item, "productId").Equals(productId, StringComparison.OrdinalIgnoreCase))
                    .Select(version =>
                    {
                        var versionNumber = BsonHelpers.GetString(version, "version");
                        var versionSoftware = software
                            .Where(item => BsonHelpers.GetString(item, "productId").Equals(productId, StringComparison.OrdinalIgnoreCase)
                                           && BsonHelpers.GetString(item, "productVersion").Equals(versionNumber, StringComparison.OrdinalIgnoreCase))
                            .OrderBy(item => BsonHelpers.GetString(item, "version"), VersionStringComparer.Descending)
                            .Select(ProductTemplatePassportBuilder.FromSoftwareDocument)
                            .ToList();
                        return ProductTemplatePassportBuilder.FromProductVersionDocument(version, template, versionSoftware);
                    })
                    .OrderBy(version => version.Version, VersionStringComparer.Descending)
                    .ToList();

                return template with
                {
                    ProductVersions = versions.Count == 0 ? template.ProductVersions : versions,
                    SoftwareVersions = versions.Count == 0
                        ? template.SoftwareVersions
                        : versions.SelectMany(version => version.SoftwareVersions)
                            .GroupBy(version => version.Version, StringComparer.OrdinalIgnoreCase)
                            .Select(group => group.First())
                            .OrderBy(version => version.Version, VersionStringComparer.Ascending)
                            .ToList()
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

    public async Task EnsureDefaultTemplatesAsync(string actor, CancellationToken cancellationToken = default)
    {
        var productCollection = ProductCollection();
        var productVersionCollection = ProductVersionCollection();
        var softwareCollection = SoftwareCollection();
        if (productCollection == null || productVersionCollection == null || softwareCollection == null)
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
            await softwareCollection.DeleteManyAsync(
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

                foreach (var software in productVersion.SoftwareVersions)
                {
                    await softwareCollection.ReplaceOneAsync(
                        Builders<BsonDocument>.Filter.And(
                            Builders<BsonDocument>.Filter.Eq("productId", product.ProductId),
                            Builders<BsonDocument>.Filter.Eq("productVersion", productVersion.Version),
                            Builders<BsonDocument>.Filter.Eq("version", software.Version)),
                        ProductTemplatePassportBuilder.ToSoftwareDocument(product.ProductId, productVersion.Version, software, now, actor),
                        new ReplaceOptions { IsUpsert = true },
                        cancellationToken);
                }

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
                version.SoftwareVersions.Any(software => software.Version.Equals(softwareVersion, StringComparison.OrdinalIgnoreCase)))
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
        var software = selectedProductVersion.SoftwareVersions.FirstOrDefault(version => version.Version.Equals(softwareVersion, StringComparison.OrdinalIgnoreCase))
            ?? selectedProductVersion.SoftwareVersions.First();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var document = ProductTemplatePassportBuilder.BuildPassportFromTemplate(passportId, product, selectedProductVersion, software, identity, now);
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
        var softwareCollection = SoftwareCollection();
        if (productCollection == null || productVersionCollection == null || softwareCollection == null)
        {
            return;
        }

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
        await softwareCollection.DeleteManyAsync(
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

            foreach (var software in productVersion.SoftwareVersions)
            {
                await softwareCollection.ReplaceOneAsync(
                    Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Eq("productId", product.ProductId),
                        Builders<BsonDocument>.Filter.Eq("productVersion", productVersion.Version),
                        Builders<BsonDocument>.Filter.Eq("version", software.Version)),
                    ProductTemplatePassportBuilder.ToSoftwareDocument(product.ProductId, productVersion.Version, software, now, actor),
                    new ReplaceOptions { IsUpsert = true },
                    cancellationToken);
            }

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
        await EnsureDefaultTemplatesAsync(actor, cancellationToken);
        await EnsureFixedClustersAndUsersAsync(cancellationToken);

        var collection = PassportCollection();
        if (collection == null)
        {
            return new ProductTemplateResetResult(0, [], DateTimeOffset.UtcNow.ToString("O"));
        }

        var existingIds = await collection
            .Find(Builders<BsonDocument>.Filter.Empty)
            .Project(Builders<BsonDocument>.Projection.Include("passportId").Exclude("_id"))
            .ToListAsync(cancellationToken);
        var allExistingIds = existingIds
            .Select(document => BsonHelpers.GetString(document, "passportId"))
            .Where(passportId => !string.IsNullOrWhiteSpace(passportId))
            .ToList();
        await _auditRevisionService.DeleteDemoLedgerAsync(allExistingIds.Concat(FixedPassportIds).ToArray(), cancellationToken);
        await collection.DeleteManyAsync(Builders<BsonDocument>.Filter.Empty, cancellationToken);

        var resetAt = DateTimeOffset.UtcNow.ToString("O");
        var seeds = new[]
        {
            ("did:web:acme.battery.pass:0226151e-949c-d067-8ef3-162431e28976", "compact-7m", "1.0", "cluster-default-demonstrator", "CP7M-DEMO-001", "SN-0226151E", "Compact 7M demonstrator battery", "DEFAULT-LINE-01"),
            ("did:web:acme.battery.pass:sample-customer-north-001", "compact-7m", "2.0", "cluster-north-operations", "CP7M-NORTH-001", "SN-NORTH-001", "North Compact 7M customer battery", "NORTH-LINE-01"),
            ("did:web:acme.battery.pass:sample-customer-south-001", "compact-13m", "2.0", "cluster-south-operations", "CP13M-SOUTH-001", "SN-SOUTH-001", "South Compact 13M customer battery", "SOUTH-LINE-01"),
            ("did:web:acme.battery.pass:sample-end-user-fleet-001", "core", "3.0", "cluster-fleet-operations", "CORE-FLEET-001", "SN-FLEET-001", "Fleet Core customer battery", "FLEET-LINE-01")
        };

        foreach (var seed in seeds)
        {
            var document = await BuildPassportFromTemplateAsync(
                seed.Item1,
                seed.Item2,
                seed.Item3,
                new ProductTemplateBatteryIdentity
                {
                    ClusterId = seed.Item4,
                    ModelNumber = seed.Item5,
                    SerialNumber = seed.Item6,
                    DisplayName = seed.Item7,
                    FacilityId = seed.Item8,
                    ManufacturingDate = resetAt[..10]
                },
                actor,
                cancellationToken);
            await _passportRepository.ReplaceAsync(seed.Item1, document, cancellationToken);

            var policy = await _dataCompletionPolicyService.GetPolicyForPassportAsync(document, cancellationToken);
            var summary = _passportValidationService.Validate(document, policy);
            var signature = _passportTrustService.Sign(document, actor);
            var revision = await _auditRevisionService.CreateSignedRevisionAsync(
                seed.Item1,
                signature.Snapshot,
                signature.Hash,
                signature.Proof,
                actor,
                signature.SignedAt,
                cancellationToken);
            var revisionId = BsonHelpers.GetString(revision, "revisionId");
            await _passportRepository.UpdateTrustSignatureAsync(seed.Item1, summary, signature.Hash, signature.Proof, revisionId, signature.SignedAt, cancellationToken);
            await _passportRepository.PublishPassportAsync(seed.Item1, revisionId, resetAt, $"sha256:{signature.Hash}", signature.Proof, cancellationToken);
            await _auditRevisionService.MarkRevisionPublishedAsync(revisionId, resetAt, cancellationToken);
            await _auditRevisionService.AppendAuditEventAsync(
                seed.Item1,
                "passport.productTemplate.seeded",
                actor,
                "admin",
                "product-template-reset",
                "Passport seeded from product template.",
                new BsonDocument
                {
                    ["productId"] = seed.Item2,
                    ["softwareVersion"] = seed.Item3,
                    ["resetAt"] = resetAt
                },
                cancellationToken);
        }

        return new ProductTemplateResetResult(seeds.Length, FixedPassportIds, resetAt);
    }

    public async Task<ProductTemplatePushResult> PushTemplateAsync(
        string productId,
        string softwareVersion,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var product = await GetProductAsync(productId, cancellationToken);
        if (product == null)
        {
            return new ProductTemplatePushResult();
        }

        var productVersion = product.ProductVersions.FirstOrDefault(version =>
                version.SoftwareVersions.Any(software => software.Version.Equals(softwareVersion, StringComparison.OrdinalIgnoreCase)))
            ?? product.LatestProductVersion;
        return await PushTemplateAsync(productId, productVersion.Version, softwareVersion, actor, cancellationToken);
    }

    public async Task<ProductTemplatePushResult> PushTemplateAsync(string productId, string productVersion, string softwareVersion, string actor, CancellationToken cancellationToken = default)
    {
        var product = await GetProductAsync(productId, cancellationToken);
        if (product == null)
        {
            return new ProductTemplatePushResult();
        }

        var selectedProductVersion = product.ProductVersions.FirstOrDefault(version => version.Version.Equals(productVersion, StringComparison.OrdinalIgnoreCase))
            ?? product.LatestProductVersion;
        var software = selectedProductVersion.SoftwareVersions.FirstOrDefault(version => version.Version.Equals(softwareVersion, StringComparison.OrdinalIgnoreCase))
            ?? selectedProductVersion.SoftwareVersions.First();
        var collection = PassportCollection();
        if (collection == null)
        {
            return new ProductTemplatePushResult();
        }

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("app.product.productId", product.ProductId),
            Builders<BsonDocument>.Filter.Eq("app.product.productVersion", selectedProductVersion.Version),
            Builders<BsonDocument>.Filter.Eq("app.product.softwareVersion", software.Version));
        var passports = await collection.Find(filter).ToListAsync(cancellationToken);
        var updatedPassportIds = new List<string>();
        var skippedPaths = new List<string>();
        var now = DateTimeOffset.UtcNow.ToString("O");

        foreach (var passport in passports)
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
                software,
                identity,
                now);
            ApplyTemplateDocumentReferences(fresh, await GetProductDocumentAsync(product.ProductId, cancellationToken));
            var newTemplate = ProductTemplatePassportBuilder.BuildTemplateBaseline(fresh);
            var result = ProductTemplatePassportBuilder.ComputeSafeTemplateUpdates(passport, oldTemplate, newTemplate);
            if (result.UpdatedPaths.Count == 0)
            {
                skippedPaths.AddRange(result.SkippedOverridePaths);
                continue;
            }

            result.UpdatedPassport.Remove("_id");
            await _passportRepository.ReplaceAsync(BsonHelpers.GetString(passport, "passportId"), result.UpdatedPassport, cancellationToken);
            await _passportRepository.MarkCanonicalDirtyAsync(BsonHelpers.GetString(passport, "passportId"), "productTemplatePushed", cancellationToken);
            updatedPassportIds.Add(BsonHelpers.GetString(passport, "passportId"));
            skippedPaths.AddRange(result.SkippedOverridePaths);
            await _auditRevisionService.AppendAuditEventAsync(
                BsonHelpers.GetString(passport, "passportId"),
                "passport.productTemplate.pushed",
                actor,
                "admin",
                "product-template-push",
                "Product template changes pushed to passport.",
                new BsonDocument
                {
                    ["productId"] = product.ProductId,
                    ["productVersion"] = selectedProductVersion.Version,
                    ["softwareVersion"] = software.Version,
                    ["updatedPaths"] = new BsonArray(result.UpdatedPaths),
                    ["skippedOverridePaths"] = new BsonArray(result.SkippedOverridePaths)
                },
                cancellationToken);
        }

        var pushRunsCollection = PushRunsCollection();
        if (pushRunsCollection != null)
        {
            await pushRunsCollection.InsertOneAsync(new BsonDocument
            {
                ["productId"] = product.ProductId,
                ["productVersion"] = selectedProductVersion.Version,
                ["softwareVersion"] = software.Version,
                ["actor"] = actor,
                ["createdAt"] = now,
                ["matchedBatteries"] = passports.Count,
                ["updatedBatteries"] = updatedPassportIds.Count,
                ["skippedOverridePaths"] = new BsonArray(skippedPaths.Distinct(StringComparer.OrdinalIgnoreCase))
            }, cancellationToken: cancellationToken);
        }

        return new ProductTemplatePushResult
        {
            MatchedBatteries = passports.Count,
            UpdatedBatteries = updatedPassportIds.Count,
            SkippedBatteries = passports.Count - updatedPassportIds.Count,
            UpdatedPassportIds = updatedPassportIds,
            SkippedOverridePaths = skippedPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private async Task EnsureFixedClustersAndUsersAsync(CancellationToken cancellationToken)
    {
        const string password = "Password123!";
        var passwordHash = BCryptNet.HashPassword(password);
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

        var passportId = Uri.EscapeDataString(BsonHelpers.GetString(passport, "passportId"));
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
                : $"/api/files/{fileId}?passportId={passportId}";
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

    private IMongoCollection<BsonDocument>? SoftwareCollection() =>
        _mongoContext.Database?.GetCollection<BsonDocument>("batteryProductTemplateSoftwareVersions");

    private IMongoCollection<BsonDocument>? PassportCollection() =>
        _mongoContext.Database?.GetCollection<BsonDocument>("passports");

    private IMongoCollection<BsonDocument>? PushRunsCollection() =>
        _mongoContext.Database?.GetCollection<BsonDocument>("batteryProductTemplatePushRuns");

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
