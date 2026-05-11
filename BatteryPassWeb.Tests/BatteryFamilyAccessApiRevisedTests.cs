using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class BatteryFamilyAccessApiRevisedTests
{
    [Fact]
    public void PassportFactory_ShouldExposeRevisedIdentityAndPassportStatus()
    {
        var passport = MinimalPassport(registryStatus: "draft", trustState: TrustState.Signed);

        var model = new PassportViewModelFactory().Create(passport);

        Assert.Equal("Compact 7M", model.BatteryFamily);
        Assert.Equal("2.0", model.BatteryVersion);
        Assert.Equal("SN-001", model.BatterySerialNumber);
        Assert.Equal("Signed", model.PassportStatus);
        Assert.Equal("Original", model.BatteryStatus);
    }

    [Fact]
    public void ProductTemplateSource_ShouldDocumentBatteryFamilyMapping()
    {
        var docs = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"))
            + File.ReadAllText(RepoFile("docs", "qa-test-pack.md"));

        Assert.Contains("Battery Family", docs);
        Assert.Contains("Product Template", docs);
        Assert.Contains("Product Template is the internal implementation name for Battery Family", docs);
    }

    [Fact]
    public void UserFacingViews_ShouldNotUseRemovedIdentityLabels()
    {
        var files = new[]
        {
            RepoFile("web", "Views", "Registry", "Index.cshtml"),
            RepoFile("web", "Views", "Passport", "Summary.cshtml"),
            RepoFile("web", "Views", "Passport", "Detail.cshtml"),
            RepoFile("web", "Views", "Admin", "EditPassport.cshtml"),
            RepoFile("web", "Views", "Admin", "Clusters.cshtml"),
            RepoFile("web", "Views", "Admin", "Product.cshtml")
        };

        var text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

        Assert.Contains("Battery Family", text);
        Assert.Contains("Battery version", text);
        Assert.Contains("Battery serial number", text);
        Assert.Contains("Passport status", text);
        Assert.Contains("Battery status", text);
        Assert.DoesNotContain(">Name <", text);
        Assert.DoesNotContain(">Model Number<", text);
        Assert.DoesNotContain("Product/battery version", text);
        Assert.DoesNotContain(">Product<", text);
        Assert.DoesNotContain("Product templates", text);
    }

    [Fact]
    public void Software_ShouldBeAParameterNotASeparateVariantWorkflow()
    {
        var modelSource = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateModels.cs"));
        var serviceSource = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));
        var apiSource = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
        var productView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));

        Assert.Contains("string SoftwareVersion", modelSource);
        Assert.Contains("string SoftwareReleaseDate", modelSource);
        Assert.Contains("string SoftwareLatestUpdate", modelSource);
        Assert.DoesNotContain("IReadOnlyList<BatteryProductSoftwareVersion> SoftwareVersions", modelSource);
        Assert.DoesNotContain("batteryProductTemplateSoftwareVersions", serviceSource);
        Assert.DoesNotContain("[HttpPatch(\"batteries/{passportId}/software\")]", apiSource);
        Assert.DoesNotContain("data-product-software", productView);
        Assert.DoesNotContain("Software versions", productView);
    }

    [Fact]
    public void ExternalApi_ShouldUseTokensOnlyAndExposeBatteryVersionValidateSign()
    {
        var api = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
        var repository = File.ReadAllText(RepoFile("web", "Services", "ExternalApiRepository.cs"));
        var initializer = File.ReadAllText(RepoFile("web", "Services", "ExternalApiInitializer.cs"));

        Assert.Contains("ExternalTokenAccessMode.Sign", repository);
        Assert.Contains("ValidateTokenAsync(token, ExternalTokenRequirement.Sign", api);
        Assert.Contains("[HttpPatch(\"batteries/{passportId}/battery-version\")]", api);
        Assert.Contains("[HttpPost(\"batteries/{passportId}/validate\")]", api);
        Assert.Contains("[HttpPost(\"batteries/{passportId}/sign\")]", api);
        Assert.DoesNotContain("X-Battery-Secret", api);
        Assert.DoesNotContain("ValidateBatterySecretAsync", api);
        Assert.DoesNotContain("UpsertBatterySecretAsync", repository);
        Assert.Contains("DropCollectionAsync(\"batterySecrets\"", repository);
        Assert.DoesNotContain("EnsureBatterySecret", initializer);
    }

    [Fact]
    public void ValidateSignPublish_ShouldUseSharedAutoRepublishWorkflow()
    {
        var admin = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var api = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
        var repository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
        var workflow = File.ReadAllText(RepoFile("web", "Services", "PassportTrustWorkflowService.cs"));

        Assert.Contains("PassportTrustWorkflowService", admin);
        Assert.Contains("PassportTrustWorkflowService", api);
        Assert.Contains("ShouldAutoPublishAfterSign", workflow);
        Assert.Contains("AutoPublished", workflow);
        Assert.Contains("registryInfo.hasBeenPublished", repository);
    }

    [Fact]
    public void AccessControl_ShouldDefineRevisedRolesAndTrustVisibility()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));

        Assert.Contains("Normal User", source);
        Assert.Contains("Notified Body", source);
        Assert.Contains("Market Surveillance Authorities", source);
        Assert.Contains("Commission", source);
        Assert.Contains("Person with Legitimate Interest", source);
        Assert.Contains("CanViewTrustConformanceAsync", source);
        Assert.Contains("marketSurveillanceAuthority", source);
        Assert.Contains("commission", source);
        Assert.DoesNotContain("notifiedBody\") ||", source);
        Assert.DoesNotContain("legitimateInterest\") ||", source);
    }

    [Fact]
    public void RegistryAndAdminViews_ShouldUseIconActionsAndRecoverableArchive()
    {
        var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
        var adminClusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var repository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("aria-label=\"Summary report\"", registry);
        Assert.Contains("title=\"Summary report\"", registry);
        Assert.Contains("aria-label=\"Detailed report\"", registry);
        Assert.Contains("title=\"Detailed report\"", registry);
        Assert.Contains("title=\"Edit\"", adminClusters);
        Assert.Contains("title=\"Conformance\"", adminClusters);
        Assert.Contains("title=\"Archive\"", adminClusters);
        Assert.Contains("title=\"Unarchive\"", adminClusters);
        Assert.Contains("return confirm('Archive passport", adminClusters);
        Assert.Contains("UnarchivePassportAsync", repository);
        Assert.Contains("[HttpPost(\"passports/unarchive\")]", controller);
    }

    [Fact]
    public void AccountAndClusterGuardrails_ShouldBePresent()
    {
        var accountController = RepoFile("web", "Controllers", "AccountController.cs");
        var accountView = RepoFile("web", "Views", "Account", "Index.cshtml");
        var clusterRepository = File.ReadAllText(RepoFile("web", "Services", "ClusterRepository.cs"));
        var passportRepository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
        var authService = File.ReadAllText(RepoFile("web", "Services", "AuthService.cs"));
        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var layout = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));

        Assert.True(File.Exists(accountController));
        Assert.True(File.Exists(accountView));
        Assert.Contains("CountPassportsByClusterAsync", passportRepository);
        Assert.Contains("CountMembershipsByClusterAsync", clusterRepository);
        Assert.Contains("ForceDeleteClusterAsync", adminController);
        Assert.Contains("I understand this deletes linked cluster data", adminController);
        Assert.Contains("CreatePrincipalForUserAsync", authService);
        Assert.Contains("UpdateUserEmailAsync", clusterRepository);
        Assert.Contains("UpdateUserProfileAsync", clusterRepository);
        Assert.Contains("Cannot change your own local admin role", clusterAdminController);
        Assert.Contains("href=\"/account\"", layout);
        Assert.Contains("@customerName / @customerRole", layout);
    }

    [Fact]
    public void LocalAdminApiTokenManagement_ShouldBeScopedToAdministeredClusters()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var view = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "ApiTokens.cshtml"));

        Assert.Contains("[HttpGet(\"api-tokens\")]", controller);
        Assert.Contains("CreateClusterApiToken", controller);
        Assert.Contains("DeleteClusterApiToken", controller);
        Assert.Contains("RegenerateClusterApiToken", controller);
        Assert.Contains("ValidateClusterTokenScopeAsync", controller);
        Assert.Contains("CanAdministerClusterAsync", controller);
        Assert.Contains("/cluster-admin/api-tokens/create", view);
        Assert.Contains("Validate + sign", view);
        Assert.Contains("/cluster-admin/api-tokens/delete", view);
    }

    private static BsonDocument MinimalPassport(string registryStatus, string trustState)
    {
        return new BsonDocument
        {
            ["passportId"] = "did:web:acme.battery.pass:test-001",
            ["registryInfo"] = new BsonDocument
            {
                ["status"] = registryStatus,
                ["updatedAt"] = "2026-05-11T00:00:00Z"
            },
            ["trust"] = new BsonDocument
            {
                ["state"] = trustState,
                ["isDirty"] = false
            },
            ["validation"] = new BsonDocument
            {
                ["isValid"] = trustState.Equals(TrustState.Signed, StringComparison.OrdinalIgnoreCase),
                ["hash"] = "hash-1"
            },
            ["app"] = new BsonDocument
            {
                ["display"] = new BsonDocument
                {
                    ["name"] = "Old display name",
                    ["modelNumber"] = "Old model",
                    ["serialNumber"] = "SN-001",
                    ["manufacturerName"] = "Scania Industrial Batteries",
                    ["facilityId"] = "LINE-1"
                },
                ["product"] = new BsonDocument
                {
                    ["productId"] = "compact-7m",
                    ["productName"] = "Compact 7M",
                    ["productVersion"] = "2.0",
                    ["softwareVersion"] = "4.0",
                    ["softwareReleaseDate"] = "2026-05-01",
                    ["softwareLatestUpdate"] = "2026-05-08"
                },
                ["media"] = new BsonDocument(),
                ["documents"] = new BsonDocument(),
                ["operations"] = new BsonDocument()
            },
            ["aspects"] = new BsonDocument
            {
                ["generalProductInformation"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryStatus"] = "Original",
                        ["batteryCategory"] = "industrial",
                        ["batteryMass"] = 320,
                        ["manufacturingDate"] = "2026-05-01T00:00:00Z"
                    }
                }
            }
        };
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(parts)}");
    }
}
